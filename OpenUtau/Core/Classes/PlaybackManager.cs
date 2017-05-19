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

namespace OpenUtau.Core
{
    class PlaybackManager : ICmdSubscriber
    {
        private WaveOut outDevice;

        private PlaybackManager() { this.Subscribe(DocManager.Inst); }

        private static PlaybackManager _s;
        public static PlaybackManager Inst { get { if (_s == null) { _s = new PlaybackManager(); } return _s; } }

        MixingSampleProvider masterMix;
        List<TrackSampleProvider> trackSources = new List<TrackSampleProvider>();
        List<TrackSampleProvider> trackSourcesRaw = new List<TrackSampleProvider>();

        public void Play(UProject project)
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

        public void StopPlayback()
        {
            if (outDevice != null) outDevice.Stop();
            SkipedTimeSpan = TimeSpan.Zero;
        }

        public void PausePlayback()
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
            if (span != TimeSpan.Zero)
            {
                outDevice.Init(masterMix.Skip(span));
            }
            else
            {
                outDevice.Init(masterMix);
            }
            outDevice.Play();
        }

        int pendingParts = 0;
        object lockObject = new object();


        private async void BuildAudioAndPlay(UProject project) {
            //BuildAudio(project);
            masterMix = await RenderDispatcher.Inst.GetMixingSampleProvider(project);
            trackSources = new List<TrackSampleProvider>(masterMix.MixerInputs.Cast<TrackSampleProvider>());
            trackSourcesRaw = DeepClone(trackSources);
            /*if (pendingParts == 0)*/
            StartPlayback(SkipedTimeSpan, true);
        }

        public void UpdatePlayPos()
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

        # region ICmdSubscriber

        public void Subscribe(ICmdPublisher publisher) { if (publisher != null) publisher.Subscribe(this); }

        public TimeSpan SkipedTimeSpan { get; private set; }

        List<TrackSampleProvider> DeepClone(List<TrackSampleProvider> src)
        {
            List<TrackSampleProvider> list = new List<TrackSampleProvider>();
            foreach (var item in src)
            {
                var cloned = new TrackSampleProvider() { Muted = item.Muted, Pan = item.Pan, PlainVolume = item.PlainVolume };
                foreach (var mixsrc in item.Sources)
                {
                    if (mixsrc is OffsetSampleProvider offset)
                    {
                        var field = typeof(OffsetSampleProvider).GetField("sourceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        var innersrc = field.GetValue(offset) as ISampleProvider;
                        cloned.AddSource(innersrc, offset.DelayBy);
                    }
                }
                list.Add(cloned);
            }
            return list;
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {

            if (cmd is SeekPlayPosTickNotification)
            {
                bool oStop = false;
                if (outDevice?.PlaybackState == PlaybackState.Stopped) oStop = true;
                StopPlayback();
                var _cmd = cmd as SeekPlayPosTickNotification;
                int tick = _cmd.playPosTick;
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick));
                if(outDevice == null || outDevice.PlaybackState != PlaybackState.Paused)
                    SkipedTimeSpan = TimeSpan.FromMilliseconds(DocManager.Inst.Project.TickToMillisecond(tick));
                if (outDevice != null && outDevice.PlaybackState == PlaybackState.Stopped && !oStop)
                {
                    trackSources = DeepClone(trackSourcesRaw);
                    outDevice.Dispose();
                    StartPlayback(SkipedTimeSpan);
                }
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
