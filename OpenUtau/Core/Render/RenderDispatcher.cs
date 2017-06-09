﻿using System;
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
        private List<TrackWaveChannel> trackCache = new List<TrackWaveChannel>();
        public async Task<UWaveMixerStream32> GetMixingStream(UProject project)
        {
            if (trackCache.Count == 0) trackCache = Enumerable.Repeat((TrackWaveChannel)null, project.Tracks.Count).ToList();
            if (trackCache.Capacity < project.Tracks.Count) trackCache.Capacity = project.Tracks.Count;
            UWaveMixerStream32 masterMix = new UWaveMixerStream32();
            List<TrackSampleProvider> trackSources = Enumerable.Repeat((TrackSampleProvider)null, project.Tracks.Count).ToList();
            foreach (UTrack track in project.Tracks)
            {
                if (track.Amended || !trackCache.Any(track1 => track1?.TrackNo == track.TrackNo))
                {
                    trackSources[track.TrackNo] = (new TrackSampleProvider() { TrackNo = track.TrackNo, PlainVolume = DecibelToVolume(track.Volume), Muted = track.Mute, Pan = (float)track.Pan / 90f });
                    track.Amended = false;
                }
                else
                    masterMix.AddInputStream(trackCache[track.TrackNo]);
            }
            foreach (UPart part in project.Parts)
            {
                if(trackSources[part.TrackNo] != null)
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
                            TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick) - project.TickToMillisecond(480, part.PosTick) * part.PosTick / project.Resolution));
                    }
                }
            }
            int i = 0;
            var schedule = new List<Task>();
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Rendering Tracks 0/{trackSources.Count}"));
            int pending = 0;
            int total = trackSources.Count;
            var pre = new List<TrackSampleProvider>(trackSources);
            foreach (var source in pre)
            {
                if (source != null)
                {
                    var str = new System.IO.MemoryStream(source.WaveFormat.AverageBytesPerSecond * 60);

                    schedule.Add(Task.Run(async () =>
                    {
                        /*while (true)
                        {
                            if (pending < 2) break;
                            await Task.Delay(1000);
                        }*/
                        ++pending;
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
                        buffer = null;
                        wave = null;
                    }).ContinueWith(task =>
                    {
                        //if (task.IsFaulted) throw task.Exception;
                        trackSources.Remove(source);
                        ++i;
                        --pending;
                        var src1 = new RawSourceWaveStream(str, source.WaveFormat);
                        var src2 = new TrackWaveChannel(src1) { TrackNo = source.TrackNo, PlainVolume = source.PlainVolume, Pan = source.Pan, Muted = source.Muted };
                        masterMix.AddInputStream(src2);
                        trackCache[src2.TrackNo] = src2;
                        DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)((float)i / total * 100), $"Rendering Tracks {i}/{total}"));
                    }));
                }
                else
                {
                    ++i;
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)((float)i / total * 100), $"Rendering Tracks {i}/{total}"));
                }
            }
            await Task.WhenAll(schedule);
            trackSources.Clear();
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, ""));
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
                                TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick) - project.TickToMillisecond(480, part.PosTick) * part.PosTick / project.Resolution));
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
            var offseted = new UOffsetSampleProvider(sample) { SkipOver = new TimeSpan(0,0,0,0,(int)project.TickToMillisecond(part.HeadTrimTick, part.PosTick - part.HeadTrimTick)), Take = new TimeSpan(0,0,0,0,(int)project.TickToMillisecond(part.DurTick, part.PosTick))};
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
