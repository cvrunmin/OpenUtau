using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core.USTx;
using OpenUtau.Core.Render;
using OpenUtau.Core.ResamplerDriver;
using OpenUtau.Core.Render.NAudio;
using System.Threading;

namespace OpenUtau.Core
{
    abstract class PlaybackManager : ICmdSubscriber
    {
        protected PlaybackManager() { Subscribe(DocManager.Inst); }

        public abstract void Play(UProject project);

        public abstract void StopPlayback();

        public abstract void PausePlayback();

        public abstract void OnNext(UCommand cmd, bool isUndo);

        public abstract void UpdatePlayPos();

        public abstract bool IsPlayingBack();

        public void Subscribe(ICmdPublisher publisher)
        {
            publisher?.Subscribe(this);
        }

        public static PlaybackManager GetActiveManager() {
            try
            {
                switch (Util.Preferences.Default.RenderManager)
                {
                    case "Instant":
                        return InstantPlaybackManager.Inst;
                    case "PreRender":
                    default:
                        return PreRenderPlaybackManager.Inst;
                }
            }
            catch (Exception)
            {
                return InstantPlaybackManager.Inst;
            }
        }
    }
    class PreRenderPlaybackManager : PlaybackManager
    {
        private WaveOut outDevice;

        private PreRenderPlaybackManager() : base() { }

        private static PreRenderPlaybackManager _s;
        public static PreRenderPlaybackManager Inst { get { if (_s == null) { _s = new PreRenderPlaybackManager(); } return _s; } }
        
        UWaveMixerStream32 masterMix;
        List<TrackWaveChannel> trackSources = new List<TrackWaveChannel>();
        private CancellationTokenSource token;
        public override void Play(UProject project)
        {
            if (pendingParts > 0) return;
            else if (outDevice != null)
            {
                if (outDevice.PlaybackState == PlaybackState.Playing) return;
                else if (outDevice.PlaybackState == PlaybackState.Paused) { outDevice.Resume(); return; }
                else outDevice.Dispose();
            }
            BuildAudioAndPlay(project);
        }

        public override bool IsPlayingBack()
        {
            return outDevice.PlaybackState == PlaybackState.Playing;
        }

        public override void StopPlayback()
        {
            if (pendingParts > 0) {
                token.Cancel();
            }
            if (outDevice != null)
            {
                outDevice.Stop();
                outDevice.Dispose();
                outDevice = null;
            }

            SkipedTimeSpan = TimeSpan.Zero;
        }

        public override void PausePlayback()
        {
            if (outDevice != null) outDevice.Pause();
        }

        private void StartPlayback(bool preMade = false)
        {
            if (outDevice != null && outDevice.PlaybackState == PlaybackState.Paused)
            {
                outDevice.Resume();
                return;
            }
            if (!preMade)
            {
                masterMix = new UWaveMixerStream32();
                foreach (var source in trackSources) masterMix.AddInputStream(source);
            }
            outDevice = new WaveOut();
            outDevice.PlaybackStopped += (sender, e) => {
                StopPlayback();
            };
            if (masterMix != null)
            {
                outDevice.Init(masterMix);
                outDevice.Play();
            }
        }

        int pendingParts = 0;
        object lockObject = new object();


        private async void BuildAudioAndPlay(UProject project)
        {
            try
            {
                pendingParts = 1;
                token = new CancellationTokenSource();
                token.Token.Register(() => DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, ""), true));
                masterMix = await RenderDispatcher.Inst.GetMixingStream(project,token.Token);
                trackSources = new List<TrackWaveChannel>(masterMix.InputStreams.Cast<TrackWaveChannel>());
            }
            catch (OperationCanceledException) { }
            finally
            {
                if(masterMix != null)
                    masterMix.CurrentTime = SkipedTimeSpan;
                pendingParts = 0;
                StartPlayback(true);
            }
        }

        public override void UpdatePlayPos()
        {
            if (outDevice != null && outDevice.PlaybackState == PlaybackState.Playing)
            {
                double ms = masterMix.CurrentTime.TotalMilliseconds;
                int tick = DocManager.Inst.Project.MillisecondToTick(ms);
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick), true);
            }
        }

        private float DecibelToVolume(double db)
        {
            return db == -24 ? 0 : db < -16 ? (float)MusicMath.DecibelToLinear(db * 2 + 16) : (float)MusicMath.DecibelToLinear(db);
        }

        public TimeSpan SkipedTimeSpan { get; private set; }

        # region ICmdSubscriber

        public override void OnNext(UCommand cmd, bool isUndo)
        {

            if (cmd is SeekPlayPosTickNotification)
            {
                var _cmd = cmd as SeekPlayPosTickNotification;
                int tick = _cmd.playPosTick;
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick));
                SkipedTimeSpan = TimeSpan.FromMilliseconds(DocManager.Inst.Project.TickToMillisecond(tick));
                if (outDevice != null)
                {
                    masterMix.CurrentTime = SkipedTimeSpan;
                }
            }
            else if (cmd is VolumeChangeNotification)
            {
                var _cmd = cmd as VolumeChangeNotification;
                if (masterMix != null && masterMix.InputCount > _cmd.TrackNo)
                {
                    (masterMix.InputStreams.Find(stream=> ((TrackWaveChannel)stream).TrackNo == _cmd.TrackNo) as TrackWaveChannel).PlainVolume = DecibelToVolume(_cmd.Volume);
                }
                if (trackSources != null && trackSources.Count > _cmd.TrackNo)
                {
                    trackSources.Find(stream => stream.TrackNo == _cmd.TrackNo).PlainVolume = DecibelToVolume(_cmd.Volume);
                }
            }
            else if (cmd is PanChangeNotification)
            {
                var _cmd = cmd as PanChangeNotification;
                if (masterMix != null && masterMix.InputCount > _cmd.TrackNo)
                {
                    (masterMix.InputStreams.Find(stream => ((TrackWaveChannel)stream).TrackNo == _cmd.TrackNo) as TrackWaveChannel).Pan = (float)_cmd.Pan / 90f;
                }
                if (trackSources != null && trackSources.Count > _cmd.TrackNo)
                {
                    trackSources.Find(stream => stream.TrackNo == _cmd.TrackNo).Pan = (float)_cmd.Pan / 90f;
                }
            }
            else if (cmd is MuteNotification mute)
            {
                if (masterMix != null && masterMix.InputCount > mute.TrackNo)
                {
                    (masterMix.InputStreams.Find(stream => ((TrackWaveChannel)stream).TrackNo == mute.TrackNo) as TrackWaveChannel).Muted = mute.Muted;
                }
                if (trackSources != null && trackSources.Count > mute.TrackNo)
                {
                    trackSources.Find(stream => stream.TrackNo == mute.TrackNo).Muted = mute.Muted;
                }
            }
        }

        # endregion
    }

    class InstantPlaybackManager : PlaybackManager
    {
        private WaveOut outDevice;

        private InstantPlaybackManager() : base() { }

        private static InstantPlaybackManager _s;
        public static InstantPlaybackManager Inst { get { if (_s == null) { _s = new InstantPlaybackManager(); } return _s; } }

        MixingSampleProvider masterMix;
        List<TrackSampleProvider> trackSources = new List<TrackSampleProvider>();
        List<TrackSampleProvider> trackSourcesRaw = new List<TrackSampleProvider>();
        private CancellationTokenSource token;
        public override void Play(UProject project)
        {
            if (pendingParts > 0) return;
            else if (outDevice != null)
            {
                if (outDevice.PlaybackState == PlaybackState.Playing) return;
                else if (outDevice.PlaybackState == PlaybackState.Paused) { outDevice.Resume(); return; }
                else outDevice.Dispose();
            }
            BuildAudioAndPlay(project);
        }

        public override bool IsPlayingBack()
        {
            return outDevice.PlaybackState == PlaybackState.Playing;
        }

        public override void StopPlayback()
        {
            if (pendingParts > 0)
            {
                token.Cancel();
            }
            if (outDevice != null) outDevice.Stop();
            SkipedTimeSpan = TimeSpan.Zero;
        }

        public override void PausePlayback()
        {
            if (outDevice != null) outDevice.Pause();
        }

        private void StartPlayback()
        {
            StartPlayback(TimeSpan.Zero);
        }

        private void StartPlayback(TimeSpan span, bool preMade = false)
        {
            if (outDevice != null && outDevice.PlaybackState == PlaybackState.Paused)
            {
                outDevice.Resume();
                return;
            }
            if (!preMade)
            {
                masterMix = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
                foreach (var source in trackSources) masterMix.AddMixerInput(source);
            }
            outDevice = new WaveOut();
            outDevice.PlaybackStopped += (sender, e) => {
                StopPlayback();
            };
            outDevice.Init(masterMix.USkip(span));
            outDevice.Play();
        }

        int pendingParts = 0;
        object lockObject = new object();


        private async void BuildAudioAndPlay(UProject project) {
            try
            {
                token = new CancellationTokenSource();
                token.Token.Register(() => DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, ""), true));
                pendingParts = 1;
                masterMix = await RenderDispatcher.Inst.GetMixingSampleProvider(project, new int[0], token.Token);
                trackSources = new List<TrackSampleProvider>(masterMix.MixerInputs.Cast<TrackSampleProvider>());
            }
            catch (OperationCanceledException) { }
            finally
            {
                pendingParts = 0;
                StartPlayback(SkipedTimeSpan, true);
            }
        }

        public override void UpdatePlayPos()
        {
            if (outDevice != null && outDevice.PlaybackState == PlaybackState.Playing)
            {
                double ms = outDevice.GetPosition() * 1000.0 / masterMix.WaveFormat.BitsPerSample /masterMix.WaveFormat.Channels * 8 / masterMix.WaveFormat.SampleRate;
                int tick = DocManager.Inst.Project.MillisecondToTick(ms + SkipedTimeSpan.TotalMilliseconds);
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick), true);
            }
        }

        private float DecibelToVolume(double db)
        {
            return db == -24 ? 0 : db < -16 ? (float)MusicMath.DecibelToLinear(db * 2 + 16) : (float)MusicMath.DecibelToLinear(db);
        }

        public TimeSpan SkipedTimeSpan { get; private set; }

        List<TrackSampleProvider> DeepClone(List<TrackSampleProvider> src)
        {
            List<TrackSampleProvider> list = new List<TrackSampleProvider>();
            foreach (var item in src)
            {
                list.Add(item.Clone());
            }
            return list;
        }

        # region ICmdSubscriber

        public override void OnNext(UCommand cmd, bool isUndo)
        {

            if (cmd is SeekPlayPosTickNotification)
            {
                StopPlayback();
                var _cmd = cmd as SeekPlayPosTickNotification;
                int tick = _cmd.playPosTick;
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick));
                SkipedTimeSpan = TimeSpan.FromMilliseconds(DocManager.Inst.Project.TickToMillisecond(tick));
            }
            else if (cmd is VolumeChangeNotification)
            {
                var _cmd = cmd as VolumeChangeNotification;
                if (masterMix != null && masterMix.MixerInputs.Count() > _cmd.TrackNo)
                {
                    (masterMix.MixerInputs.ElementAt(_cmd.TrackNo) as TrackSampleProvider).PlainVolume = DecibelToVolume(_cmd.Volume);
                }
                if (trackSources != null && trackSources.Count > _cmd.TrackNo)
                {
                    trackSources[_cmd.TrackNo].PlainVolume = DecibelToVolume(_cmd.Volume);
                }
                if (trackSourcesRaw != null && trackSourcesRaw.Count > _cmd.TrackNo)
                {
                    trackSourcesRaw[_cmd.TrackNo].PlainVolume = DecibelToVolume(_cmd.Volume);
                }
            }
            else if (cmd is PanChangeNotification)
            {
                var _cmd = cmd as PanChangeNotification;
                if (masterMix != null && masterMix.MixerInputs.Count() > _cmd.TrackNo)
                {
                    (masterMix.MixerInputs.ElementAt(_cmd.TrackNo) as TrackSampleProvider).Pan = (float)_cmd.Pan / 90f;
                }
                if (trackSources != null && trackSources.Count > _cmd.TrackNo)
                {
                    trackSources[_cmd.TrackNo].Pan = (float)_cmd.Pan / 90f;
                }
                if (trackSourcesRaw != null && trackSourcesRaw.Count > _cmd.TrackNo)
                {
                    trackSourcesRaw[_cmd.TrackNo].Pan = (float)_cmd.Pan / 90f;
                }
            }
            else if (cmd is MuteNotification mute)
            {
                if (masterMix != null && masterMix.MixerInputs.Count() > mute.TrackNo)
                {
                    (masterMix.MixerInputs.ElementAt(mute.TrackNo) as TrackSampleProvider).Muted = mute.Muted;

                    //(masterMix.MixerInputs.ElementAt(mute.TrackNo) as TrackSampleProvider).Volume = mute.Muted ? 0 : DecibelToVolume(trackSources[mute.TrackNo].Volume);
                }
                if (trackSources != null && trackSources.Count > mute.TrackNo)
                {
                    trackSources[mute.TrackNo].Muted = mute.Muted;
                }
                if (trackSourcesRaw != null && trackSourcesRaw.Count > mute.TrackNo)
                {
                    trackSourcesRaw[mute.TrackNo].Muted = mute.Muted;
                }
            }
        }

        # endregion
    }
}
