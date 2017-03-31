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
            if (!preMade)
            {
                masterMix = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
                foreach (var source in trackSources) masterMix.AddMixerInput(source);
            }
            outDevice = new WaveOut();
            if (span != TimeSpan.Zero)
            {
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
            /*if (pendingParts == 0)*/ StartPlayback(TimeSpan.Zero, true);
        }

        public void UpdatePlayPos()
        {
            if (outDevice != null && outDevice.PlaybackState == PlaybackState.Playing)
            {
                double ms = outDevice.GetPosition() * 1000.0 / masterMix.WaveFormat.BitsPerSample /masterMix.WaveFormat.Channels * 8 / masterMix.WaveFormat.SampleRate;
                int tick = DocManager.Inst.Project.MillisecondToTick(ms);
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick), true);
            }
        }

        private float DecibelToVolume(double db)
        {
            return db == -24 ? 0 : db < -16 ? (float)MusicMath.DecibelToLinear(db * 2 + 16) : (float)MusicMath.DecibelToLinear(db);
        }

        # region ICmdSubscriber

        public void Subscribe(ICmdPublisher publisher) { if (publisher != null) publisher.Subscribe(this); }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is SeekPlayPosTickNotification)
            {
                StopPlayback();
                var _cmd = cmd as SeekPlayPosTickNotification;
                int tick = _cmd.playPosTick;
                DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick));
                //if (_cmd.project != null)
                    StartPlayback(new TimeSpan(tick), true);
            }
            else if (cmd is VolumeChangeNotification)
            {
                var _cmd = cmd as VolumeChangeNotification;
                if (masterMix != null && masterMix.MixerInputs.Count() > _cmd.TrackNo) {
                    (masterMix.MixerInputs.ElementAt(_cmd.TrackNo) as TrackSampleProvider).Volume = DecibelToVolume(_cmd.Volume);
                }
                if (trackSources != null && trackSources.Count > _cmd.TrackNo)
                {
                    trackSources[_cmd.TrackNo].Volume = DecibelToVolume(_cmd.Volume);
                }
            }
        }

        # endregion
    }
}
