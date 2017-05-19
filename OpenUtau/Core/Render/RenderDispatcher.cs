using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core.ResamplerDriver;

namespace OpenUtau.Core.Render
{
    class RenderDispatcher
    {
        public List<RenderItem> RenderItems = new List<RenderItem>();

        private static RenderDispatcher _s;
        public static RenderDispatcher Inst { get { if (_s == null) { _s = new RenderDispatcher(); } return _s; } }
        public RenderDispatcher() { }

        public async void WriteToFile(string file)
        {
            await Task.Run(()=> WaveFileWriter.CreateWaveFile16(file, GetMixingSampleProvider())).ContinueWith(task=>DocManager.Inst.ExecuteCmd(new ProgressBarNotification(100, "Done!")));
        }

        public async void WriteToFile(string file, UProject project)
        {
            await Task.Run(async ()=>WaveFileWriter.CreateWaveFile(file, (await GetMixingSampleProvider(project)).ToWaveProvider())).ContinueWith(task=>DocManager.Inst.ExecuteCmd(new ProgressBarNotification(100, "Done!")));
        }

        public SequencingSampleProvider GetMixingSampleProvider()
        {
            List<RenderItemSampleProvider> segmentProviders = new List<RenderItemSampleProvider>();
            foreach (var item in RenderItems) segmentProviders.Add(new RenderItemSampleProvider(item));
            return new SequencingSampleProvider(segmentProviders);
        }

        object lockObject = new object();
        public async Task<MixingSampleProvider> GetMixingSampleProvider(UProject project)
        {
            return await GetMixingSampleProvider(project, new int[] { });
        }
        public async Task<MixingSampleProvider> GetMixingSampleProvider(UProject project, int[] skippedTracks)
        {
            return await GetMixingSampleProvider(project, skippedTracks, System.Threading.CancellationToken.None);
        }
        public async Task<MixingSampleProvider> GetMixingSampleProvider(UProject project, int[] skippedTracks, System.Threading.CancellationToken cancel) {
            MixingSampleProvider masterMix;
            List<TrackSampleProvider> trackSources;
            trackSources = new List<TrackSampleProvider>();
            foreach (UTrack track in project.Tracks)
            {
                if(!skippedTracks.Contains(track.TrackNo))
                    trackSources.Add(new TrackSampleProvider() { PlainVolume = DecibelToVolume(track.Volume), Muted = track.Mute, Pan = (float)track.Pan / 90f });
                cancel.ThrowIfCancellationRequested();
            }
            foreach (UPart part in project.Parts)
            {
                if (skippedTracks.Contains(part.TrackNo)) continue;
                if (part is UWavePart)
                {
                    lock (lockObject)
                    {
                        var src = BuildWavePartAudio(part as UWavePart, project);
                        if(src != null)
                        trackSources[part.TrackNo].AddSource(
                            src,
                            TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)));
                    }
                }
                else
                {
                    var singer = project.Tracks[part.TrackNo].Singer;
                    if (singer != null && singer.Loaded)
                    {
                        System.IO.FileInfo ResamplerFile = new System.IO.FileInfo(PathManager.Inst.GetPreviewEnginePath());
                        IResamplerDriver engine = ResamplerDriver.ResamplerDriver.LoadEngine(ResamplerFile.FullName);
                            trackSources[part.TrackNo].AddSource(await BuildVoicePartAudio(part as UVoicePart, project, engine, cancel) ?? new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider(),
                                TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick) - project.TickToMillisecond(480) * part.PosTick / project.Resolution));
                    }
                }
                cancel.ThrowIfCancellationRequested();
            }
            masterMix = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            foreach (var source in trackSources)
            {
                masterMix.AddMixerInput(source);
                cancel.ThrowIfCancellationRequested();
            }
            return masterMix;
        }
        private ISampleProvider BuildWavePartAudio(UWavePart part, UProject project)
        {
            AudioFileReader stream;
            try { stream = new AudioFileReader(part.FilePath); }
            catch { return null; }
            var sample = new WaveToSampleProvider(stream);
            if (sample.WaveFormat.SampleRate != 44100)
            {
                return new WdlResamplingSampleProvider(sample, 44100);
            }
            return sample;
        }

        private Task<SequencingSampleProvider> BuildVoicePartAudio(UVoicePart part, UProject project, IResamplerDriver engine) {
            return BuildVoicePartAudio(part, project, engine, System.Threading.CancellationToken.None);
        }
        private Task<SequencingSampleProvider> BuildVoicePartAudio(UVoicePart part, UProject project, IResamplerDriver engine, System.Threading.CancellationToken cancel)
        {
            ResamplerInterface ri = new ResamplerInterface();
            return ri.ResamplePartNew(part, project, engine);
        }

        private float DecibelToVolume(double db)
        {
            return db == -24 ? 0 : db < -16 ? (float)MusicMath.DecibelToLinear(db * 2 + 16) : (float)MusicMath.DecibelToLinear(db);
        }
    }
}
