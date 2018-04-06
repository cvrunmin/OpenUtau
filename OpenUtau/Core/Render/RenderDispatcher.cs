using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core.ResamplerDriver;
using OpenUtau.Core.Render.NAudio;
using System.Threading;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.Render
{
    class RenderDispatcher
    {
        public List<RenderItem> RenderItems = new List<RenderItem>();

        private static RenderDispatcher _s;
        public static RenderDispatcher Inst { get { if (_s == null) { _s = new RenderDispatcher(); } return _s; } }
        public RenderDispatcher() {

        }

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
        public List<(UWaveMixerStream32 Raw, TrackWaveChannel Baked)> trackCache = new List<(UWaveMixerStream32, TrackWaveChannel)>();
        public Dictionary<int, WaveStream> partCache = new Dictionary<int, WaveStream>();

        public Task<UWaveMixerStream32> GetMixingStream(UProject project) {
            return GetMixingStream(project, CancellationToken.None);
        }
        public async Task<UWaveMixerStream32> GetMixingStream(UProject project, CancellationToken token)
        {
            if (trackCache.Count == 0) trackCache = Enumerable.Repeat(((UWaveMixerStream32, TrackWaveChannel))(null,null), project.Tracks.Count).ToList();
            if (trackCache.Capacity < project.Tracks.Count || trackCache.Count < project.Tracks.Count)
            {
                trackCache.Capacity = project.Tracks.Count;
                trackCache.AddRange(Enumerable.Repeat(((UWaveMixerStream32, TrackWaveChannel))(null, null), trackCache.Capacity - trackCache.Count));
                token.ThrowIfCancellationRequested();
            }
            int i = 0;
            int total = project.Tracks.Count;
            var schedule = new List<Task>();
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(-1, $"Checking cache"));
            if(!Preferences.Default.UseScript)
            foreach (var item in project.Tracks.Select(track=>track.Singer).Distinct())
            {
                schedule.Add(Task.Run(() => SoundbankCache.MakeSingerCache(item)));
            }
            await Task.WhenAll(schedule);
            schedule.Clear();
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Rendering Tracks 0/{total}"));
            UWaveMixerStream32 masterMix = new UWaveMixerStream32();
            List<TrackSampleProvider> trackSources = Enumerable.Repeat((TrackSampleProvider)null, project.Tracks.Count).ToList();
            var parts = project.Parts.GroupBy(part => part.TrackNo).ToDictionary(group => group.Key);
            foreach (UTrack track in project.Tracks)
            {
                schedule.Add(Task.Run(async() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (track.Amended || !trackCache.Any(track1 => track1.Baked?.TrackNo == track.TrackNo))
                    {
                        trackCache.ForEach(channel =>
                        {
                            if ((channel.Baked?.TrackNo ?? -1) == track.TrackNo)
                            {
                                channel.Raw?.RemoveInputStreams();
                                channel.Baked?.Dispose();
                            }
                        });
                        var trackMixing = new UWaveMixerStream32();
                        trackSources[track.TrackNo] = (new TrackSampleProvider() { TrackNo = track.TrackNo, PlainVolume = DecibelToVolume(track.Volume), Muted = track.ActuallyMuted, Pan = (float)track.Pan / 90f });
                        var singer = track.Singer;
                        var subschedule = new List<Task>();
                        if (!parts.TryGetValue(track.TrackNo, out var group)) return;
                        foreach (var part in group)
                        {
                            subschedule.Add(Task.Run(() =>
                            {
                                if (!partCache.ContainsKey(part.PartNo) || part.Amended)
                                {
                                    if (partCache.ContainsKey(part.PartNo))partCache[part.PartNo]?.Dispose();
                                    if (part is UWavePart)
                                    {
                                        WaveStream s;
                                        lock (lockObject)
                                        {
                                            s = BuildWavePartSAudio(part as UWavePart, project);
                                        }
                                        if (s != null)
                                        {
                                            if (!partCache.ContainsKey(part.PartNo))
                                            {
                                                partCache.Add(part.PartNo, s);
                                            }
                                            else
                                            {
                                                partCache[part.PartNo] = s;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (singer != null && singer.Loaded)
                                        {
                                            System.IO.FileInfo ResamplerFile = new System.IO.FileInfo(string.IsNullOrWhiteSpace(track.OverrideRenderEngine) ? PathManager.Inst.GetPreviewEnginePath() : System.IO.Path.Combine(PathManager.Inst.GetEngineSearchPath(), track.OverrideRenderEngine));
                                            IResamplerDriver engine = ResamplerDriver.ResamplerDriver.LoadEngine(ResamplerFile.FullName);
                                            BuildVoicePart(project, part as UVoicePart, engine, token).ContinueWith(task =>
                                            {
                                                DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)((float)i / total * 100), $"Rendering Tracks {i}/{total}"));
                                                try
                                                {

                                                    if (!task.IsCanceled && !task.Result.IsFaulted && !task.Result.IsCanceled && !task.Result.IsFaulted && task.Result.Result != null)
                                                    {
                                                        var s = task.Result.Result;
                                                        if (!partCache.ContainsKey(part.PartNo))
                                                        {
                                                            partCache.Add(part.PartNo, s);
                                                        }
                                                        else
                                                        {
                                                            partCache[part.PartNo] = s;
                                                        }
                                                    }

                                                }
                                                catch (AggregateException e) when (e.InnerException is TaskCanceledException)
                                                {

                                                }
                                            }).Wait();
                                        }
                                    }
                                    token.ThrowIfCancellationRequested();
                                }
                                WaveStream src;
                                try
                                {
                                    src = partCache[part.PartNo];
                                }
                                catch (Exception)
                                {
                                    src = new SilenceProvider(trackMixing.WaveFormat).ToWaveStream();
                                }
                                trackMixing.AddInputStream(new UWaveOffsetStream(src, TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)), TimeSpan.Zero, src.TotalTime));
                                part.ModifyCount = 0;
                            }, token));
                        }
                        await Task.WhenAll(subschedule);
                        token.ThrowIfCancellationRequested();
                        DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)((float)i / total * 100), $"Rendering Tracks {i}/{total}"));
                        var source = trackSources[track.TrackNo];
                        var src2 = new TrackWaveChannel(trackMixing) { TrackNo = source.TrackNo, PlainVolume = source.PlainVolume, Pan = source.Pan, Muted = source.Muted };
                        trackCache[src2.TrackNo] = (trackMixing, src2);
                        track.ModifyCount = 0;
                    }
                    token.ThrowIfCancellationRequested();
                    ++i;
                    masterMix.AddInputStream(trackCache[track.TrackNo].Baked);
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)((float)i / total * 100), $"Rendering Tracks {i}/{total}"));
                }, token));
                token.ThrowIfCancellationRequested();
            }
            await Task.WhenAll(schedule);
            trackSources.Clear();
            SoundbankCache.FlushCachedSingers();
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, ""));
            return masterMix;
        }

        private async Task<Task<WaveStream>> BuildVoicePart(UProject project, UVoicePart part, IResamplerDriver engine, CancellationToken token)
        {
            /*if (Preferences.Default.UseScript)
            {
                return Task.Run(() => DocManager.Inst.Factory.StartNew(()=>
                {
                    var batch = Path.Combine(DocManager.CachePath, $"temp-Part_{part.PartNo}.bat");
                    UtauRenderScript.GenerateTempBat(batch, project, part);
                    var p = Process.Start(new ProcessStartInfo(batch) { UseShellExecute = false, CreateNoWindow = true });
                    p.WaitForExit();
                    try
                    {
                        return (WaveStream)new AudioFileReader(Path.Combine(DocManager.CachePath, $"temp-Part_{part.PartNo}.wav"));
                    }
                    catch (Exception e)
                    {
                        return WritePartProviderToStream(
                            new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider(), part.PartNo, CancellationToken.None);
                    }
                }));
            }
            else*/
            bool usescript = Preferences.Default.UseScript;
            return await Task.Run(()=>BuildVoicePartAudio(part, project, engine, token, usescript)).ContinueWith(async task =>
            {
                if (!task.IsCanceled)
                {
                    if (Preferences.Default.UseScript)
                    {
                        try
                        {
                            return new AudioFileReaderExt(Path.Combine(DocManager.CachePath, $"temp-Part_{part.PartNo}.wav"));
                        }
                        catch (Exception e)
                        {
                            return WritePartProviderToStream(
                                new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider(), part.PartNo, CancellationToken.None);
                        }
                    }
                    else
                    {
                        ISampleProvider src = task.Result ?? new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider();
                        return await (DocManager.Inst.Factory.StartNew(() => WritePartProviderToStream(src, part.PartNo, token)));
                    }
                }
                return null;
            });
        }

        public async Task<TrackWaveChannel> RenderTrackStream(UProject project, UTrack track, CancellationToken token, bool slient = false, int i = 0, int total = 0) {
            var parts = project.Parts.Where(part => part.TrackNo == track.TrackNo);
            var trackMixing = new UWaveMixerStream32();
            var source = (new TrackSampleProvider() { TrackNo = track.TrackNo, PlainVolume = DecibelToVolume(track.Volume), Muted = track.ActuallyMuted, Pan = (float)track.Pan / 90f });
            var singer = track.Singer;
            var subschedule = new List<Task>();
            foreach (var part in parts)
            {
                if (part is UWavePart)
                {
                    WaveStream src;
                    lock (lockObject)
                    {
                        src = BuildWavePartSAudio(part as UWavePart, project);
                    }
                    if (src != null)
                    {
                        trackMixing.AddInputStream(new UWaveOffsetStream(src, TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)), TimeSpan.Zero, src.TotalTime));
                    }
                }
                else
                {
                    if (singer != null && singer.Loaded)
                    {
                        System.IO.FileInfo ResamplerFile = new System.IO.FileInfo(string.IsNullOrWhiteSpace(track.OverrideRenderEngine) ? PathManager.Inst.GetPreviewEnginePath() : System.IO.Path.Combine(PathManager.Inst.GetEngineSearchPath(), track.OverrideRenderEngine));
                        IResamplerDriver engine = ResamplerDriver.ResamplerDriver.LoadEngine(ResamplerFile.FullName);
                        subschedule.Add(BuildVoicePartAudio(part as UVoicePart, project, engine, token).ContinueWith(async task => {
                            if (!task.IsCanceled)
                            {
                                ISampleProvider src = task.Result ?? new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider();
                                return await DocManager.Inst.Factory.StartNew(() => WritePartProviderToStream(src, part.PartNo, token), token);
                            }
                            return null;
                        }, token).ContinueWith(task => {
                            DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)((float)i / total * 100), $"Rendering Tracks {i}/{total}"));
                            try
                            {

                                if (!task.IsCanceled && !task.Result.IsFaulted && !task.Result.IsCanceled && !task.Result.IsFaulted && task.Result.Result != null)
                                {
                                    var s = task.Result.Result;
                                    trackMixing.AddInputStream(new UWaveOffsetStream(s, TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)), TimeSpan.Zero, s.TotalTime));
                                }

                            }
                            catch (AggregateException e) when (e.InnerException is TaskCanceledException)
                            {

                            }
                        }));
                    }
                }
                token.ThrowIfCancellationRequested();
            }
            await Task.WhenAll(subschedule);
            token.ThrowIfCancellationRequested();
            if(!slient)DocManager.Inst.ExecuteCmd(new ProgressBarNotification((int)((float)i / total * 100), $"Rendering Tracks {i}/{total}"));
            return new TrackWaveChannel(trackMixing) { TrackNo = source.TrackNo, PlainVolume = source.PlainVolume, Pan = source.Pan, Muted = source.Muted };
        }

        public Task<MixingSampleProvider> GetMixingSampleProvider(UProject project, int[] skippedTracks)
        {
            return GetMixingSampleProvider(project, skippedTracks, CancellationToken.None);
        }
        public async Task<MixingSampleProvider> GetMixingSampleProvider(UProject project, int[] skippedTracks, CancellationToken token) {
            MixingSampleProvider masterMix = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            List<TrackSampleProvider> trackSources = Enumerable.Repeat((TrackSampleProvider)null, project.Tracks.Count).ToList();
            var parts = project.Parts.GroupBy(part => part.TrackNo).SkipWhile(group => skippedTracks.Contains(group.Key)).ToDictionary(group => group.Key);
            var schedule = new List<Task>();
            foreach (var item in project.Tracks.Select(track => track.Singer).Distinct())
            {
                SoundbankCache.MakeSingerCache(item);
            }
            foreach (UTrack track in project.Tracks.SkipWhile(track=>skippedTracks.Contains(track.TrackNo)))
            {
                schedule.Add(Task.Run(async() => {
                    token.ThrowIfCancellationRequested();
                    trackSources[track.TrackNo] = (new TrackSampleProvider() { TrackNo = track.TrackNo, PlainVolume = DecibelToVolume(track.Volume), Muted = track.ActuallyMuted, Pan = (float)track.Pan / 90f });
                    var subschedule = new List<Task>();
                    var singer = track.Singer;
                    if (!parts.TryGetValue(track.TrackNo, out var group)) return;
                    foreach (var part in group)
                    {
                        if (part is UWavePart)
                        {
                            lock (lockObject)
                            {
                                var src = BuildWavePartAudio(part as UWavePart, project);
                                if (src != null)
                                    trackSources[part.TrackNo].AddSource(
                                        src,
                                        TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)));
                            }
                        }
                        else
                        {
                            if (singer != null && singer.Loaded)
                            {
                                System.IO.FileInfo ResamplerFile = new System.IO.FileInfo(string.IsNullOrWhiteSpace(track.OverrideRenderEngine) ? PathManager.Inst.GetPreviewEnginePath() : System.IO.Path.Combine(PathManager.Inst.GetEngineSearchPath(), track.OverrideRenderEngine));
                                IResamplerDriver engine = ResamplerDriver.ResamplerDriver.LoadEngine(ResamplerFile.FullName);
                                subschedule.Add(BuildVoicePartAudio(part as UVoicePart, project, engine, token).ContinueWith(task => {
                                    trackSources[part.TrackNo].AddSource(task.Result ?? new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider(),
                                        TimeSpan.FromMilliseconds(project.TickToMillisecond(part.PosTick)/* - project.TickToMillisecond(480, part.PosTick) * part.PosTick / project.Resolution*/));
                                }));
                            }
                        }
                        token.ThrowIfCancellationRequested();
                    }
                    await Task.WhenAll(subschedule);
                },token).ContinueWith(task => { if (!task.IsCanceled)
                        masterMix.AddMixerInput(trackSources[track.TrackNo]);
                }));
                token.ThrowIfCancellationRequested();
            }
            await Task.WhenAll(schedule);
            SoundbankCache.FlushCachedSingers();
            return masterMix;
        }

        private WaveStream BuildWavePartSAudio(UWavePart part, UProject project)
        {
            WaveStream stream;
            try
            {
                stream = new AudioFileReaderExt(part.FilePath);
            }
            catch
            {
                return new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToWaveStream(); 
            }
            var offseted = new UWaveOffsetStream(stream) { SourceOffset = TimeSpan.FromMilliseconds((int)project.TickToMillisecond(part.HeadTrimTick, part.PosTick - part.HeadTrimTick)), SourceLength = TimeSpan.FromMilliseconds((int)project.TickToMillisecond(part.DurTick, part.PosTick)) };
            return offseted;
        }

        private ISampleProvider BuildWavePartAudio(UWavePart part, UProject project)
        {
            WaveStream stream;
            try { stream = new AudioFileReaderExt(part.FilePath); }
            catch { return new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)).ToSampleProvider(); }
            ISampleProvider sample;
            if (stream.WaveFormat.SampleRate != 44100)
            {
                //stream = new WaveFormatConversionStream(new WaveFormat(44100, stream.WaveFormat.BitsPerSample, stream.WaveFormat.Channels), stream);
                sample = new WdlResamplingSampleProvider(stream.ToSampleProvider(), 44100);
            }
            else
            {
                sample = stream.ToSampleProvider();
            }
            var offseted = new UOffsetSampleProvider(sample) { SkipOver = new TimeSpan(0,0,0,0,(int)project.TickToMillisecond(part.HeadTrimTick, part.PosTick - part.HeadTrimTick)), Take = new TimeSpan(0,0,0,0,(int)project.TickToMillisecond(part.DurTick, part.PosTick))};
            return offseted;
        }

        private Task<SequencingSampleProvider> BuildVoicePartAudio(UVoicePart part, UProject project, IResamplerDriver engine, bool useWavtool = false) {
            return BuildVoicePartAudio(part, project, engine, System.Threading.CancellationToken.None, useWavtool);
        }
        private async Task<SequencingSampleProvider> BuildVoicePartAudio(UVoicePart part, UProject project, IResamplerDriver engine, System.Threading.CancellationToken cancel, bool useWavtool = false)
        {
            ResamplerInterface ri = new ResamplerInterface();
            return await ri.ResamplePartNew(part, project, engine, cancel, useWavtool);
        }

        private float DecibelToVolume(double db)
        {
            return db == -24 ? 0 : db < -16 ? (float)MusicMath.DecibelToLinear(db * 2 + 16) : (float)MusicMath.DecibelToLinear(db);
        }

        public static WaveStream WritePartProviderToStream(ISampleProvider source, int partNo, CancellationToken token) {
            double elisimatedMs;
            try
            {
                elisimatedMs = DocManager.Inst.Project.TickToMillisecond(DocManager.Inst.Project.Parts[partNo].DurTick, DocManager.Inst.Project.Parts[partNo].PosTick);
            }
            catch (Exception)
            {
                elisimatedMs = 60000;
            }

            int limit = partNo < 0 ? 2147483591 : source.WaveFormat.AverageBytesPerSecond * (int)Math.Ceiling(elisimatedMs / 1000);
            var str = new System.IO.MemoryStream(limit);
            var wave = source.ToWaveProvider();
            var buffer = new byte[source.WaveFormat.AverageBytesPerSecond];
            var pos = 0;
            while (pos < limit)
            {
                if (token.IsCancellationRequested) break;
                if (2147483591 - str.Position < buffer.Length)
                {
                    buffer = new byte[2147483591 - str.Position - 1];
                    var bytesRead1 = wave.Read(buffer, 0, buffer.Length);
                    if (bytesRead1 == 0)
                    {
                        // end of source provider
                        str.Flush();
                        break;
                    }
                    str.Write(buffer, 0, bytesRead1);
                    break;
                }
                var bytesRead = wave.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // end of source provider
                    str.Flush();
                    break;
                }
                str.Write(buffer, 0, bytesRead);
                pos += bytesRead;
            }
            buffer = null;
            wave = null;
            token.ThrowIfCancellationRequested();
            str.Position = 0;
            var src1 = new RawSourceWaveStream(str, source.WaveFormat);
            return src1;
        }
    }
}
