using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core.ResamplerDriver;
using OpenUtau.Core.Render.NAudio;

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
        public async Task<UWaveMixerStream32> GetMixingStream(UProject project)
        {
            UWaveMixerStream32 masterMix;
            List<TrackSampleProvider> trackSources;
            trackSources = new List<TrackSampleProvider>();
            foreach (UTrack track in project.Tracks)
            {
                //if(!skippedTracks.Contains(track.TrackNo))
                trackSources.Add(new TrackSampleProvider() { TrackNo = track.TrackNo, PlainVolume = DecibelToVolume(track.Volume), Muted = track.Mute, Pan = (float)track.Pan / 90f });
            }
            foreach (UPart part in project.Parts)
            {
                if (part is UWavePart)
                {
                    lock (lockObject)
                    {
                        var src = BuildWavePartAudio(part as UWavePart, project);
                        if (src != null)
                        {
                            trackSources[part.TrackNo].AddSource(
                                src,
                                TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)));
                        }
                    }
                }
                else
                {
                    var singer = project.Tracks[part.TrackNo].Singer;
                    if (singer != null && singer.Loaded)
                    {
                        System.IO.FileInfo ResamplerFile = new System.IO.FileInfo(PathManager.Inst.GetPreviewEnginePath());
                        IResamplerDriver engine = ResamplerDriver.ResamplerDriver.LoadEngine(ResamplerFile.FullName);
                        trackSources[part.TrackNo].AddSource(await BuildVoicePartAudio(part as UVoicePart, project, engine) ?? new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider(),
                            TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick) - project.TickToMillisecond(480) * part.PosTick / project.Resolution));
                    }
                }
            }
            int i = 0;
            masterMix = new UWaveMixerStream32();
            var schedule = new List<Task>();
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Rendering Tracks 0/{trackSources.Count}"));
            foreach (var source in trackSources)
            {
                var str = new System.IO.MemoryStream();

                schedule.Add(Task.Run(async () => {
                    var wave = source.ToWaveProvider();
                    var buffer = new byte[source.WaveFormat.AverageBytesPerSecond * 4];
                    while (true)
                    {
                        var bytesRead = wave.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            // end of source provider
                            str.Flush();
                            break;
                        }
                        await str.WriteAsync(buffer, 0, bytesRead);
                    }
                }).ContinueWith(task=> {
                    if (task.IsFaulted) {
                        throw task.Exception;
                    }
                    ++i;
                    var src1 = new RawSourceWaveStream(str, source.WaveFormat);
                    var src2 = new TrackWaveChannel(src1) { TrackNo = source.TrackNo, PlainVolume = source.PlainVolume, Pan = source.Pan, Muted = source.Muted };
                    masterMix.AddInputStream(src2);
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)((float)i / trackSources.Count * 100), $"Rendering Tracks {i}/{trackSources.Count}"));
                }));
            }
            await Task.WhenAll(schedule);
            return masterMix;
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
                //if(!skippedTracks.Contains(track.TrackNo))
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
            WaveStream stream;
            try { stream = new AudioFileReader(part.FilePath); }
            catch { return null; }
            if (stream.WaveFormat.SampleRate != 44100) {
                stream = new WaveFormatConversionStream(new WaveFormat(44100, stream.WaveFormat.BitsPerSample, stream.WaveFormat.Channels), stream);
            }
            ISampleProvider sample = new WaveToSampleProvider(stream);
            var offseted = new UOffsetSampleProvider(sample) { SkipOver = new TimeSpan(0,0,0,0,(int)project.TickToMillisecond(part.HeadTrimTick)), Take = new TimeSpan(0,0,0,0,(int)project.TickToMillisecond(part.DurTick))};
            return offseted;
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
