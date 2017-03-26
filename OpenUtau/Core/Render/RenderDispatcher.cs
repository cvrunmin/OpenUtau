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

        public void WriteToFile(string file)
        {
            WaveFileWriter.CreateWaveFile16(file, GetMixingSampleProvider());
        }

        public async void WriteToFile(string file, UProject project)
        {
            WaveFileWriter.CreateWaveFile(file, (await GetMixingSampleProvider(project)).ToWaveProvider());
        }

        public SequencingSampleProvider GetMixingSampleProvider()
        {
            List<RenderItemSampleProvider> segmentProviders = new List<RenderItemSampleProvider>();
            foreach (var item in RenderItems) segmentProviders.Add(new RenderItemSampleProvider(item));
            return new SequencingSampleProvider(segmentProviders);
        }

        object lockObject = new object();
        public async Task<MixingSampleProvider> GetMixingSampleProvider(UProject project) {
            MixingSampleProvider masterMix;
            List<TrackSampleProvider> trackSources;
            trackSources = new List<TrackSampleProvider>();
            foreach (UTrack track in project.Tracks)
            {
                trackSources.Add(new TrackSampleProvider() { Volume = DecibelToVolume(track.Volume) });
            }
            List<Task> tasks = new List<Task>();
            foreach (UPart part in project.Parts)
            {
                if (part is UWavePart)
                {
                    lock (lockObject)
                    {
                        trackSources[part.TrackNo].AddSource(
                            BuildWavePartAudio(part as UWavePart, project),
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
                        //lock (lockObject)
                        {
                            trackSources[part.TrackNo].AddSource(await BuildVoicePartAudio(part as UVoicePart, project, engine) ?? new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider(),
                                TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)));
                        }
                    }
                }
            }
            //Task.WaitAll(tasks.ToArray());
            masterMix = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            foreach (var source in trackSources) masterMix.AddMixerInput(source);
            return masterMix;
        }
        private ISampleProvider BuildWavePartAudio(UWavePart part, UProject project)
        {
            AudioFileReader stream;
            try { stream = new AudioFileReader(part.FilePath); }
            catch { return null; }
            return new WaveToSampleProvider(stream);
        }

        private Task<SequencingSampleProvider> BuildVoicePartAudio(UVoicePart part, UProject project, IResamplerDriver engine)
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
