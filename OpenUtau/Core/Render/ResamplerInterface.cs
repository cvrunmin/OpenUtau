using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;

using OpenUtau.Core.USTx;
using OpenUtau.Core.ResamplerDriver;

namespace OpenUtau.Core.Render
{
    class ResamplerInterface
    {
        Action<SequencingSampleProvider> resampleDoneCallback;

        public void ResamplePart(UVoicePart part, UProject project, IResamplerDriver engine, Action<SequencingSampleProvider> resampleDoneCallback)
        {
            this.resampleDoneCallback = resampleDoneCallback;
            BackgroundWorker worker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };
            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerAsync(new Tuple<UVoicePart, UProject, IResamplerDriver>(part, project, engine));
        }

        public Task<SequencingSampleProvider> ResamplePartNew(UVoicePart part, UProject project, IResamplerDriver engine) {
            return ResamplePartNew(part, project, engine, System.Threading.CancellationToken.None);
        }
        public Task<SequencingSampleProvider> ResamplePartNew(UVoicePart part, UProject project, IResamplerDriver engine, System.Threading.CancellationToken cancel)
        {
            return DocManager.Inst.Factory.StartNew(() => RenderAsync(part, project, engine, cancel), cancel)
                .ContinueWith(task =>
                {
                    List<RenderItemSampleProvider> renderItemSampleProviders = new List<RenderItemSampleProvider>();
                    if (task.Status == TaskStatus.RanToCompletion)
                        foreach (var item in task.Result) renderItemSampleProviders.Add(new RenderItemSampleProvider(item));
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, ""));
                    try
                    {
                        return new SequencingSampleProvider(renderItemSampleProviders);
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                });
        }

        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(e.ProgressPercentage, (string)e.UserState));
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var args = e.Argument as Tuple<UVoicePart, UProject, IResamplerDriver>;
            var part = args.Item1;
            var project = args.Item2;
            var engine = args.Item3;
            e.Result = RenderAsync(part, project, engine, sender as BackgroundWorker);
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            List<RenderItem> renderItems = e.Result as List<RenderItem>;
            List<RenderItemSampleProvider> renderItemSampleProviders = new List<RenderItemSampleProvider>();
            foreach (var item in renderItems) renderItemSampleProviders.Add(new RenderItemSampleProvider(item));
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, ""));
            resampleDoneCallback(new SequencingSampleProvider(renderItemSampleProviders));
        }
        private List<RenderItem> RenderAsync(UVoicePart part, UProject project, IResamplerDriver engine) {
            return RenderAsync(part, project, engine, System.Threading.CancellationToken.None);
        }
        private List<RenderItem> RenderAsync(UVoicePart part, UProject project, IResamplerDriver engine, System.Threading.CancellationToken cancel)
        {
            List<RenderItem> renderItems = new List<RenderItem>();
            Debug.Assert(engine != null, "Engine is not provided");
            System.Diagnostics.Stopwatch watch = new Stopwatch();
            watch.Start();
            System.Diagnostics.Debug.WriteLine("Resampling start");
            lock (part)
            {
                try
                {
                    string cacheDir = PathManager.Inst.GetCachePath(project.FilePath);
                    int count = 0, i = 0;
                    foreach (UNote note in part.Notes) foreach (UPhoneme phoneme in note.Phonemes) count++;

                    foreach (UNote note in part.Notes)
                    {
                        foreach (UPhoneme phoneme in note.Phonemes)
                        {
                            cancel.ThrowIfCancellationRequested();
                            RenderItem item = BuildRenderItem(phoneme, part, project);
                            if (!item.Error)
                            {
                                RenderPhoneme(engine, cacheDir, item, Path.GetFileNameWithoutExtension(project.FilePath), part.TrackNo);
                                if (item.Sound != null) renderItems.Add(item);
                            }
                            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(100 * ++i / count, string.Format("Resampling \"{0}\" {1}/{2}", phoneme.Phoneme, i, count)));
                        }
                        cancel.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                }

            }
            watch.Stop();
            System.Diagnostics.Debug.WriteLine("Resampling end");
            System.Diagnostics.Debug.WriteLine("Total cache size {0:n0} bytes", RenderCache.Inst.TotalMemSize);
            System.Diagnostics.Debug.WriteLine("Total time {0} ms", watch.Elapsed.TotalMilliseconds);
            return renderItems;
        }
        private List<RenderItem> RenderAsync(UVoicePart part, UProject project, IResamplerDriver engine, BackgroundWorker worker)
        {
            List<RenderItem> renderItems = new List<RenderItem>();
                Debug.Assert(engine != null, "Engine is not provided");
            System.Diagnostics.Stopwatch watch = new Stopwatch();
            watch.Start();
            System.Diagnostics.Debug.WriteLine("Resampling start");
            lock (part)
            {
                string cacheDir = PathManager.Inst.GetCachePath(project.FilePath);
                string[] cacheFiles = Directory.EnumerateFiles(cacheDir).ToArray();
                int count = 0, i = 0;
                foreach (UNote note in part.Notes) foreach (UPhoneme phoneme in note.Phonemes) count++;

                foreach (UNote note in part.Notes)
                {
                    foreach (UPhoneme phoneme in note.Phonemes)
                    {
                        RenderItem item = BuildRenderItem(phoneme, part, project);
                        if (!item.Error)
                        {
                            RenderPhoneme(engine, cacheDir, item, Path.GetFileNameWithoutExtension(project.FilePath),part.TrackNo);
                            renderItems.Add(item);
                        }
                        worker.ReportProgress(100 * ++i / count, string.Format("Resampling \"{0}\" {1}/{2}", phoneme.Phoneme, i, count));
                    }
                }
            }
            watch.Stop();
            System.Diagnostics.Debug.WriteLine("Resampling end");
            System.Diagnostics.Debug.WriteLine("Total cache size {0:n0} bytes", RenderCache.Inst.TotalMemSize);
            System.Diagnostics.Debug.WriteLine("Total time {0} ms", watch.Elapsed.TotalMilliseconds);
            return renderItems;
        }
        public static IEnumerable<RenderItem> RenderNote(UProject project, UVoicePart part, UNote note) {

            System.IO.FileInfo ResamplerFile = new System.IO.FileInfo(PathManager.Inst.GetPreviewEnginePath());
            IResamplerDriver engine = ResamplerDriver.ResamplerDriver.LoadEngine(ResamplerFile.FullName);
            var inst = new ResamplerInterface();
            foreach (UPhoneme phoneme in note.Phonemes)
            {
                RenderItem item = inst.BuildRenderItem(phoneme, part, project);
                if (!item.Error)
                {
                    RenderPhoneme(engine, PathManager.Inst.GetCachePath(project.FilePath), item, Path.GetFileNameWithoutExtension(project.FilePath), part.TrackNo);
                }
                yield return item;
            }
        }
        private static void RenderPhoneme(IResamplerDriver engine, string cacheDir, RenderItem item, string projectName, int track)
        {
            var sound = RenderCache.Inst.Get(item.HashParameters(), engine.GetInfo().ToString());

            if (sound == null)
            {
                string cachefile = Path.Combine(cacheDir, $"{projectName}-Track_{track}-{item.HashParameters():x}.wav");
                if (!File.Exists(cachefile) || new FileInfo(cachefile).Length == 0)
                {
                    Debug.WriteLine("Sound {0:x} resampling {1}", item.HashParameters(), item.GetResamplerExeArgs());
                    DriverModels.EngineInput engineArgs = DriverModels.CreateInputModel(item, 0);
                    Stream output = engine.DoResampler(engineArgs);
                    if (output.Length > 0)
                    {
                        try
                        {
                            using (var deststr = File.Create(cachefile))
                            {
                                output.Seek(0, SeekOrigin.Begin);
                                output.CopyTo(deststr);
                                output.Seek(0, SeekOrigin.Begin);
                            }
                        }
                        catch (IOException)
                        {
                            Debug.WriteLine($"unable to write file for Sound {item.HashParameters():x} ({Path.GetFileName(item.RawFile ?? "")})");
                        }
                        sound = new CachedSound(output);
                    }
                }
                else
                {
                    Debug.WriteLine("Sound {0:x} found on disk {1}", item.HashParameters(), item.GetResamplerExeArgs());
                    sound = new CachedSound(cachefile);
                }
                RenderCache.Inst.Put(item.HashParameters(), sound, engine.GetInfo().ToString());
            }
            else Debug.WriteLine("Sound {0} found in cache {1}", item.HashParameters(), item.GetResamplerExeArgs());

            item.Sound = sound;
        }

        private RenderItem BuildRenderItem(UPhoneme phoneme, UVoicePart part, UProject project)
        {
            USinger singer = project.Tracks[part.TrackNo].Singer;
            bool err = false;
            string rawfile = "";
            if (singer == null) err = true;
            else { 
                rawfile = Lib.EncodingUtil.ConvertEncoding(singer.FileEncoding, singer.PathEncoding, phoneme.Oto?.File);
                if (string.IsNullOrWhiteSpace(rawfile)) err = true;
                rawfile = Path.Combine(singer.Path, rawfile);
            }
            double strechRatio = Math.Pow(2, 1.0 - (double)(int)phoneme.Parent.Expressions["velocity"].Data / 100);
            double length = (phoneme.Oto?.Preutter).GetValueOrDefault() * strechRatio + phoneme.Envelope.Points[4].X;
            double requiredLength = Math.Ceiling(length / 50 + 1) * 50;
            double lengthAdjustment = phoneme.TailIntrude == 0 ? phoneme.Preutter : phoneme.Preutter - phoneme.TailIntrude + phoneme.TailOverlap;

            RenderItem item = new RenderItem()
            {
                Error = err,
                // For resampler
                RawFile = SoundbankCache.GetSoundCachePath(rawfile, singer),
                NoteNum = phoneme.Parent.NoteNum,
                Velocity = (int)phoneme.Parent.Expressions["velocity"].Data,
                Volume = (int)phoneme.Parent.Expressions["volume"].Data,
                StrFlags = phoneme.Parent.GetResamplerFlags(),
                PitchData = BuildPitchData(phoneme, part, project),
                RequiredLength = (int)requiredLength,
                Oto = phoneme.Oto,
                Tempo = project.BPM,

                // For connector
                SkipOver = (phoneme.Oto?.Preutter).GetValueOrDefault() * strechRatio - phoneme.Preutter,
                PosMs = project.TickToMillisecond(part.PosTick + phoneme.Parent.PosTick + phoneme.PosTick) - phoneme.Preutter,
                DurMs = project.TickToMillisecond(phoneme.DurTick, part.PosTick + phoneme.Parent.PosTick + phoneme.PosTick) + lengthAdjustment,
                Envelope = phoneme.Envelope.Points
            };

            return item;
        }

        private List<int> BuildPitchData(UPhoneme phoneme, UVoicePart part, UProject project)
        {
            List<int> pitches = new List<int>();
            UNote lastNote = part.Notes.Where(x => x.CompareTo(phoneme.Parent) < 0).LastOrDefault();
            UNote nextNote = part.Notes.Where(x => x.CompareTo(phoneme.Parent) > 0).FirstOrDefault();
            // Get relevant pitch points
            List<PitchPoint> pps = new List<PitchPoint>();

            bool lastNoteInvolved = lastNote != null && phoneme.Overlapped;
            bool nextNoteInvolved = nextNote != null && nextNote.Phonemes[0].Overlapped;

            double lastVibratoStartMs = 0;
            double lastVibratoEndMs = 0;
            double vibratoStartMs = 0;
            double vibratoEndMs = 0;

            if (lastNoteInvolved)
            {
                double offsetMs = DocManager.Inst.Project.TickToMillisecond(phoneme.Parent.PosTick - lastNote.PosTick, part.PosTick + lastNote.PosTick);
                foreach (PitchPoint pp in lastNote.PitchBend.Points)
                {
                    var newpp = pp.Clone();
                    newpp.X -= offsetMs;
                    newpp.Y -= (phoneme.Parent.NoteNum - lastNote.NoteNum) * 10;
                    pps.Add(newpp);
                }
                if (lastNote.Vibrato.Depth != 0)
                {
                    lastVibratoStartMs = -DocManager.Inst.Project.TickToMillisecond(lastNote.DurTick, part.PosTick + lastNote.PosTick) * lastNote.Vibrato.Length / 100;
                    lastVibratoEndMs = 0;
                }
            }

            foreach (PitchPoint pp in phoneme.Parent.PitchBend.Points) pps.Add(pp);
            if (phoneme.Parent.Vibrato.Depth != 0)
            {
                vibratoEndMs = DocManager.Inst.Project.TickToMillisecond(phoneme.Parent.DurTick, part.PosTick + phoneme.Parent.PosTick);
                vibratoStartMs = vibratoEndMs * (1 - phoneme.Parent.Vibrato.Length / 100);
            }

            if (nextNoteInvolved)
            {
                double offsetMs = DocManager.Inst.Project.TickToMillisecond(phoneme.Parent.PosTick - nextNote.PosTick, part.PosTick + nextNote.PosTick);
                foreach (PitchPoint pp in nextNote.PitchBend.Points)
                {
                    var newpp = pp.Clone();
                    newpp.X -= offsetMs;
                    newpp.Y -= (phoneme.Parent.NoteNum - nextNote.NoteNum) * 10;
                    pps.Add(newpp);
                }
            }

            double startMs = DocManager.Inst.Project.TickToMillisecond(phoneme.PosTick, part.PosTick + phoneme.Parent.PosTick) - (phoneme.Oto?.Preutter).GetValueOrDefault();
            double endMs = DocManager.Inst.Project.TickToMillisecond(phoneme.PosTick + phoneme.DurTick, part.PosTick + phoneme.Parent.PosTick) -
                (nextNote != null && nextNote.Phonemes[0].Overlapped ? nextNote.Phonemes[0].Preutter - nextNote.Phonemes[0].Overlap : 0);
            if (pps.Count > 0)
            {
                if (pps.First().X > startMs) pps.Insert(0, new PitchPoint(startMs, pps.First().Y));
                if (pps.Last().X < endMs) pps.Add(new PitchPoint(endMs, pps.Last().Y));
            }
            else
            {
                throw new Exception("Zero pitch points.");
            }

            // Interpolation
            const int intervalTick = 1;
            double intervalMs = DocManager.Inst.Project.TickToMillisecond(intervalTick, part.PosTick + phoneme.Parent.PosTick);
            double currMs = startMs;
            int i = 0;

            while (currMs < endMs)
            {
                while (pps[i + 1].X < currMs) i++;
                double pit = MusicMath.InterpolateShape(pps[i].X, pps[i + 1].X, pps[i].Y, pps[i + 1].Y, currMs, pps[i].Shape);
                pit *= 10;

                // Apply vibratos
                if (currMs < lastVibratoEndMs && currMs >= lastVibratoStartMs)
                    pit += InterpolateVibrato(lastNote.Vibrato, currMs - lastVibratoStartMs);

                if (currMs < vibratoEndMs && currMs >= vibratoStartMs)
                    pit += InterpolateVibrato(phoneme.Parent.Vibrato, currMs - vibratoStartMs);

                pitches.Add((int)pit);
                currMs += intervalMs;
            }

            return pitches;
        }

        private double InterpolateVibrato(VibratoExpression vibrato, double posMs)
        {
            double lengthMs = vibrato.Length / 100 * DocManager.Inst.Project.TickToMillisecond(vibrato.Parent.DurTick, DocManager.Inst.Project.Parts[vibrato.Parent.PartNo].PosTick + vibrato.Parent.PosTick);
            double inMs = lengthMs * vibrato.In / 100;
            double outMs = lengthMs * vibrato.Out / 100;

            double value = -Math.Sin(2 * Math.PI * (posMs / vibrato.Period + vibrato.Shift / 100)) * vibrato.Depth;
            if(posMs >= 5 && posMs < lengthMs - 5) value += vibrato.Drift / 100 * vibrato.Depth;

            if (posMs < inMs) value *= posMs / inMs;
            else if (posMs > lengthMs - outMs) value *= (lengthMs - posMs) / outMs;

            return value;
        }
    }
}
