using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenUtau.Core.USTx;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.Render
{
    public class UtauRenderScript
    {
        public static void GenerateTempHelperBat(string path)
        {
            using (var sw = new StreamWriter(File.Create(path)))
            {
                sw.WriteLine(@"@if exist %temp% goto A");
                sw.WriteLine(@"@""%resamp%"" %1 %temp% %2 %vel% %flag% %5 %6 %7 %8 %params%");
                sw.WriteLine(@":A");
                sw.WriteLine(@"@if %9 EQU %endpt% (""%tool%"" ""%output%"" %temp% %stp% %3 %env% LAST_NOTE) else (""%tool%"" ""%output%"" %temp% %stp% %3 %env%)");
            }
        }

        public static void GenerateTempBat(string path, UProject project, UVoicePart part)
        {
            GenerateTempHelperBat(Path.Combine(Path.GetDirectoryName(path), $"temp_helper-{part.PartNo}.bat"));
            using (var sw = new StreamWriter(File.Create(path)))
            {
                #region BATCH HEADER
                sw.WriteLine($"@rem project={project.Name}-{part.Name}");
                sw.WriteLine($"@set loadmodule=");
                sw.WriteLine($"@set tempo={project.BPM}");
                sw.WriteLine($"@set samples=44100");
                sw.WriteLine($"@set oto={project.Tracks[part.TrackNo].Singer.Path}");
                sw.WriteLine($"@set tool={Preferences.Default.ScriptWavtool}");
                sw.WriteLine($"@set resamp={PathManager.Inst.GetPreviewEnginePath()}");
                sw.WriteLine($@"@set helper={Path.Combine(Path.GetDirectoryName(path), $"temp_helper-{part.PartNo}.bat")}");
                sw.WriteLine($@"@set output={Path.Combine(Path.GetDirectoryName(path), $"temp-Part_{part.PartNo}.wav")}");
                sw.WriteLine($"@set cachedir={Path.Combine(Path.GetDirectoryName(project.FilePath ?? path), "UCache")}");
                sw.WriteLine($"@set flag=\"\"");
                sw.WriteLine($"@set env=0 5 35 0 100 100 0");
                sw.WriteLine($"@set stp=0");
                #endregion
                sw.WriteLine();
                sw.WriteLine(@"@del ""%output%"" 2>nul");
                sw.WriteLine(@"@mkdir ""%cachedir%"" 2>nul");
                sw.WriteLine();
                // Length = Length@Tempo+Corr value, Corr value = Preutter - Preutter of next note + Overlap of next note
                var list = new List<UNote>();
                Formats.Ust.MakeUstNotes(project, list, part.PosTick, part);
                var set = new SortedSet<UNote>();
                foreach (var note in list)
                {
                    set.Add(note);
                }
                PartManager.UpdateOverlapAdjustment(set, part.PosTick);
                var ri = new List<RenderItem>();
                foreach (var item in set.SelectMany(note => note.Phonemes))
                {
                    ri.Add(ResamplerInterface.BuildRenderItem(item, part, project, true));
                }
                #region RenderNote
                var c = 0;
                sw.WriteLine($"@set endpt={ri.Count - 1}");
                foreach (var item in ri)
                {
                    c++;
                    if (item.Oto?.File.EndsWith("R.wav") ?? true)
                    {
                        sw.WriteLine($@"@""%tool%"" ""%output%"" ""%oto%\R.wav"" 0 {item.DurTick * 4}@{item.Tempo}{item.LengthAdjustment:+#.###;-#.###;+0} 0 0");
                    }
                    else if(!item.Error)
                    {
                        sw.WriteLine($@"@set params={item.Volume} {item.Modulation} !{item.Tempo} {Base64.Base64EncodeInt12(item.PitchData.ToArray())}");
                        sw.WriteLine($@"@set flag=""{item.StrFlags}""");
                        sw.WriteLine($@"@set env={item.Envelope[0].X - item.Envelope[0].X} {item.Envelope[1].X - item.Envelope[0].X} {item.Phoneme.TailIntrude} {item.Envelope[0].Y} {item.Envelope[1].Y} {item.Envelope[3].Y} {item.Envelope[4].Y} {item.Overlap} {item.RequiredLength - item.Envelope[4].X} {item.Envelope[2].X - item.Envelope[0].X} {item.Envelope[2].Y}");
                        sw.WriteLine($@"@set vel={item.Velocity}");
                        sw.WriteLine($@"@set temp=""{ResamplerInterface.GetCacheFile(Path.Combine(Path.GetDirectoryName(project.FilePath ?? path), "UCache"), item, string.IsNullOrWhiteSpace(project.FilePath) ? "temp" : Path.GetFileNameWithoutExtension(project.FilePath), part.TrackNo, c - 1)}""");
                        sw.WriteLine($@"@echo {new string('#', 40 * c / ri.Count)}{new string('-', 40 - 40 * c / ri.Count)}({c}/{ri.Count})");
                        sw.WriteLine($@"@call ""%helper%"" ""%oto%\{item.Oto?.File}"" {MusicMath.GetNoteString(item.NoteNum)} {item.DurTick * 4}@{item.Tempo}{item.LengthAdjustment:+#.###;-#.###;+0} {item.Phoneme.Preutter} {item.Oto?.Offset} {item.RequiredLength:D} {item.Oto.Consonant} {item.Oto.Cutoff} {c - 1}");
                    }
                }
                #endregion
                #region FOOTER
                sw.WriteLine();
                sw.WriteLine(@"@if not exist ""%output%.whd"" goto E");
                sw.WriteLine(@"@if not exist ""%output%.dat"" goto E");
                sw.WriteLine(@"copy /Y ""%output%.whd"" /B + ""%output%.dat"" /B ""%output%""");
                sw.WriteLine(@"del ""%output%.whd""");
                sw.WriteLine(@"del ""%output%.dat""");
                sw.WriteLine(@":E");
                #endregion
            }
        }
    }
}
