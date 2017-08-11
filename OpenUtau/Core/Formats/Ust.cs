using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using OpenUtau.Core.USTx;
using OpenUtau.Core.Lib;

namespace OpenUtau.Core.Formats
{
    public static class Ust
    {
        private enum UstVersion { Early, V1_0, V1_1, V1_2, Unknown };
        private enum UstBlock { Version, Setting, Note, Trackend, None };

        private const string versionTag = "[#VERSION]";
        private const string settingTag = "[#SETTING]";
        private const string endTag = "[#TRACKEND]";

        static public void Load(string[] files)
        {
            bool ustTracks = true;
            foreach (string file in files)
            {
                if (OpenUtau.Core.Formats.Formats.DetectProjectFormat(file) != Core.Formats.ProjectFormats.Ust) { ustTracks = false; break; }
            }

            if (!ustTracks)
            {
                DocManager.Inst.ExecuteCmd(new UserMessageNotification("Multiple files must be all Ust files"));
                return;
            }

            List<UProject> projects = new List<UProject>();
            foreach (string file in files)
            {
                projects.Add(Load(file));
            }

            double bpm = projects.First().BPM;
            UProject project = new UProject() { BPM = bpm, Name = "Merged Project", Saved = false };
            project.RegisterExpression(new IntExpression(null, "velocity", "VEL") { Data = 100, Min = 0, Max = 200 });
            project.RegisterExpression(new IntExpression(null, "volume", "VOL") { Data = 100, Min = 0, Max = 200 });
            project.RegisterExpression(new IntExpression(null, "gender", "GEN") { Data = 0, Min = -100, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "breathiness", "BRE") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "lowpass", "LPF") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "highpass", "HPF") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "accent", "ACC") { Data = 100, Min = 0, Max = 200 });
            project.RegisterExpression(new IntExpression(null, "decay", "DEC") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "release", "REL") { Data = 0, Min = 0, Max = 100 });
            foreach (UProject p in projects)
            {
                var _track = p.Tracks[0];
                var _part = p.Parts[0];
                _track.TrackNo = project.Tracks.Count;
                _part.TrackNo = _track.TrackNo;
                project.Tracks.Add(_track);
                project.Parts.Add(_part);
            }

            if (project != null) DocManager.Inst.ExecuteCmd(new LoadProjectNotification(project));
        }

        static public UProject Load(string file, Encoding encoding = null)
        {
            int currentNoteIndex = 0;
            UstVersion version = UstVersion.Early;
            UstBlock currentBlock = UstBlock.None;
            string[] lines;

            try
            {
                if (encoding == null) lines = File.ReadAllLines(file, EncodingUtil.DetectFileEncoding(file));
                else lines = File.ReadAllLines(file, encoding);
            }
            catch (Exception e)
            {
                DocManager.Inst.ExecuteCmd(new UserMessageNotification(e.GetType().ToString() + "\n" + e.Message));
                return null;
            }

            UProject project = new UProject() { Resolution = 480, FilePath = file, Saved = false };
            project.RegisterExpression(new IntExpression(null, "velocity","VEL") { Data = 100, Min = 0, Max = 200});
            project.RegisterExpression(new IntExpression(null, "volume","VOL") { Data = 100, Min = 0, Max = 200});
            project.RegisterExpression(new IntExpression(null, "gender","GEN") { Data = 0, Min = -100, Max = 100});
            project.RegisterExpression(new IntExpression(null, "breathiness", "BRE") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "lowpass","LPF") { Data = 0, Min = 0, Max = 100});
            project.RegisterExpression(new IntExpression(null, "highpass", "HPF") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "accent", "ACC") { Data = 100, Min = 0, Max = 200 });
            project.RegisterExpression(new IntExpression(null, "decay", "DEC") { Data = 0, Min = 0, Max = 100 });
            project.RegisterExpression(new IntExpression(null, "release", "REL") { Data = 0, Min = 0, Max = 100 });

            var _track = new UTrack();
            project.Tracks.Add(_track);
            _track.TrackNo = 0;
            var part = project.CreateVoicePart(0, 0);

            List<string> currentLines = new List<string>();
            int currentTick = 0;
            UNote currentNote = null;

            foreach (string line in lines)
            {
                if (line.Trim().StartsWith(@"[#") && line.Trim().EndsWith(@"]"))
                {
                    if (line.Equals(versionTag)) currentBlock = UstBlock.Version;
                    else if (line.Equals(settingTag)) currentBlock = UstBlock.Setting;
                    else
                    {
                        if (line.Equals(endTag)) currentBlock = UstBlock.Trackend;
                        else
                        {
                            try { currentNoteIndex = int.Parse(line.Replace("[#", "").Replace("]", "")); }
                            catch { DocManager.Inst.ExecuteCmd(new UserMessageNotification("Unknown ust format")); return null; }
                            currentBlock = UstBlock.Note;
                        }

                        if (currentLines.Count != 0)
                        {
                            currentNote = NoteFromUst(project.CreateNote(), currentLines, version);
                            currentNote.DurTick /= project.BeatPerBar;
                            currentNote.PosTick = currentTick;
                            if (!currentNote.Lyric.Replace("R", "").Replace("r", "").Equals("")) part.Notes.Add(currentNote);
                            currentTick += currentNote.DurTick;
                            currentLines.Clear();
                        }
                    }
                }
                else
                {
                    if (currentBlock == UstBlock.Version) {
                        if (line.StartsWith("UST Version"))
                        {
                            string v = line.Trim().Replace("UST Version", "");
                            if (v == "1.0") version = UstVersion.V1_0;
                            else if (v == "1.1") version = UstVersion.V1_1;
                            else if (v == "1.2") version = UstVersion.V1_2;
                            else version = UstVersion.Unknown;
                        }
                    }
                    if (currentBlock == UstBlock.Setting)
                    {
                        if (line.StartsWith("Tempo="))
                        {
                            project.BPM = double.Parse(line.Trim().Replace("Tempo=", ""));
                            if (project.BPM == 0) project.BPM = 120;
                        }
                        if (line.StartsWith("ProjectName=")) project.Name = line.Trim().Replace("ProjectName=", "");
                        if (line.StartsWith("VoiceDir="))
                        {
                            string singerpath = line.Trim().Replace("VoiceDir=", "");
                            var singer = UtauSoundbank.GetSinger(singerpath, EncodingUtil.DetectFileEncoding(file), DocManager.Inst.Singers);
                            if (singer == null) singer = new USinger() { Name = "", Path = singerpath };
                            project.Singers.Add(singer);
                            project.Tracks[0].Singer = singer;
                        }
                        if (line.StartsWith("Flags=")) {
                            var flags = line.Trim().Replace("Flags=", "");
                            var current = "";
                            var partstring = "";
                            for (int i = 0; i < flags.Length; i++)
                            {
                                char c = flags[i];
                                switch (c)
                                {
                                    case 'g':
                                        if (!string.IsNullOrWhiteSpace(current)) {
                                            Util.Utils.SetExpresstionValue(part.Expressions[current], partstring);
                                            partstring = "";
                                        }
                                        current = "gender";
                                        break;
                                    case 'Y':
                                        if (!string.IsNullOrWhiteSpace(current))
                                        {
                                            Util.Utils.SetExpresstionValue(part.Expressions[current], partstring);
                                            partstring = "";
                                        }
                                        current = "breathiness";
                                        break;
                                    case 't':
                                    case 'B':
                                        if (!string.IsNullOrWhiteSpace(current))
                                        {
                                            Util.Utils.SetExpresstionValue(part.Expressions[current], partstring);
                                            partstring = "";
                                            current = "";
                                        }
                                        break;
                                    default:
                                        partstring = string.Concat(partstring, c);
                                        break;
                                }
                            }
                        }
                    }
                    else if (currentBlock == UstBlock.Note)
                    {
                        currentLines.Add(line);
                    }
                    else if (currentBlock == UstBlock.Trackend)
                    {
                        break;
                    }
                }
            }

            if (currentBlock != UstBlock.Trackend)
                DocManager.Inst.ExecuteCmd(new UserMessageNotification("Unexpected ust file end"));
            part.DurTick = currentTick;
            project.Parts.Add(part);
            return project;
        }

        static public void Save(string pathFormat, UProject project, bool exportTrack, bool addRest, Encoding encoding = null)
        {
            //var validPath = pathFormat.SkipWhile(c => Path.GetInvalidFileNameChars().Contains(c));
            var parts = project.Parts.OfType<UVoicePart>();
            if (exportTrack)
            {
                foreach (var track in project.Tracks)
                {
                    var tParts = parts.Where(part => part.TrackNo == track.TrackNo).OrderBy(part => part.PosTick);
                    using (var file = File.CreateText(pathFormat + $"_Track-{track.TrackNo}.ust"))
                    {
                        file.WriteLine(versionTag);
                        file.WriteLine("UST Version1.2");
                        file.WriteLine(settingTag);
                        file.WriteLine("Tempo=" + project.BPM);
                        file.WriteLine("Tracks=1");
                        file.WriteLine("ProjectName=" + project.Name + "-" + track.Name);
                        file.WriteLine("VoiceDir=" + track.Singer.Path);
                        //file.WriteLine("OutFile="+project.OutputDir);
                        file.WriteLine("CacheDir=" + project.CacheDir);
                        List<UNote> writeNotes = new List<UNote>();
                        if ((tParts.FirstOrDefault()?.PosTick ?? 0) > 0)
                        {
                            var note = project.CreateNote(60, 0, tParts.FirstOrDefault().PosTick);
                            note.Lyric = "R";
                            note.Phonemes[0].Phoneme = "R";
                            writeNotes.Add(note);
                        }
                        int endtick = 0;
                        int pos = 0;
                        foreach (var part in tParts)
                        {
                            foreach (var note in part.Notes)
                            {
                                if (note.PosTick > endtick)
                                {
                                    var note1 = project.CreateNote(60, 0, note.PosTick - endtick);
                                    note1.Lyric = "R";
                                    note1.Phonemes[0].Phoneme = "R";
                                    writeNotes.Add(note1);
                                }
                                foreach (var pho in note.Phonemes)
                                {
                                    var nnote = note.Clone();
                                    nnote.DurTick = pho.DurTick;
                                    nnote.PosTick = note.PosTick + pho.PosTick;
                                    nnote.Phonemes.Clear();
                                    nnote.Phonemes.Add(pho.Clone(nnote));
                                    writeNotes.Add(nnote);
                                }
                                endtick = note.EndTick;
                            }
                        }
                        foreach (var note in writeNotes)
                        {
                            file.WriteLine($"[#{pos,4}]");
                            note.DurTick *= project.BeatPerBar;
                            note.Phonemes[0].DurTick *= project.BeatPerBar;
                            NoteToUst(note, UstVersion.V1_2).ForEach(item1 => file.WriteLine(item1));
                            ++pos;
                        }
                        file.WriteLine(endTag);
                    }
                }
            }
            else
            {
                foreach (var part in parts)
                {
                    using (var file = File.CreateText(pathFormat + $"_Track-{part.TrackNo}_Part-{part.PartNo}.ust"))
                    {
                        file.WriteLine(versionTag);
                        file.WriteLine("UST Version1.2");
                        file.WriteLine(settingTag);
                        file.WriteLine("Tempo=" + project.BPM);
                        file.WriteLine("Tracks=1");
                        file.WriteLine("ProjectName=" + project.Name + "-" + part.Name);
                        file.WriteLine("VoiceDir=" + project.Tracks[part.TrackNo].Singer.Path);
                        //file.WriteLine("OutFile="+project.OutputDir);
                        file.WriteLine("CacheDir=" + project.CacheDir);
                        List<UNote> writeNotes = new List<UNote>();
                        if (addRest && part.PosTick > 0)
                        {
                            var note = project.CreateNote(60, 0, part.PosTick);
                            note.Lyric = "R";
                            note.Phonemes[0].Phoneme = "R";
                            writeNotes.Add(note);
                        }
                        int endtick = 0;
                        int pos = 0;
                        foreach (var note in part.Notes)
                        {
                            if (note.PosTick > endtick)
                            {
                                var note1 = project.CreateNote(60, 0, note.PosTick - endtick);
                                note1.Lyric = "R";
                                note1.Phonemes[0].Phoneme = "R";
                                writeNotes.Add(note1);
                            }
                            foreach (var pho in note.Phonemes)
                            {
                                var nnote = note.Clone();
                                nnote.DurTick = pho.DurTick;
                                nnote.PosTick = note.PosTick + pho.PosTick;
                                nnote.Phonemes.Clear();
                                nnote.Phonemes.Add(pho.Clone(nnote));
                                writeNotes.Add(nnote);
                            }
                            endtick = note.EndTick;
                        }
                        foreach (var note in writeNotes)
                        {
                            file.WriteLine($"[#{pos,4}]");
                            note.DurTick *= project.BeatPerBar;
                            note.Phonemes[0].DurTick *= project.BeatPerBar;
                            NoteToUst(note, UstVersion.V1_2).ForEach(item1 => file.WriteLine(item1));
                            ++pos;
                        }
                        file.WriteLine(endTag);
                    }
                }
            }
        }

        static UNote NoteFromUst(UNote note, List<string> lines, UstVersion version)
        {
            string pbs = "", pbw = "", pby = "", pbm = "";

            foreach (string line in lines)
            {
                if (line.StartsWith("Lyric="))
                {
                    note.Phonemes[0].Phoneme = note.Lyric = line.Trim().Replace("Lyric=", "");
                    if (note.Phonemes[0].Phoneme.StartsWith("?"))
                    {
                        note.Phonemes[0].Phoneme = note.Phonemes[0].Phoneme.Substring(1);
                        note.Phonemes[0].AutoRemapped = false;
                    }
                }
                if (line.StartsWith("Length=")) note.DurTick = int.Parse(line.Trim().Replace("Length=", ""));
                if (line.StartsWith("NoteNum=")) note.NoteNum = int.Parse(line.Trim().Replace("NoteNum=", ""));
                if (line.StartsWith("Velocity=")) note.Expressions["velocity"].Data = int.Parse(line.Trim().Replace("Velocity=", ""));
                if (line.StartsWith("Intensity=")) note.Expressions["volume"].Data = int.Parse(line.Trim().Replace("Intensity=", ""));
                if (line.StartsWith("PreUtterance="))
                {
                    if (line.Trim() == "PreUtterance=") note.Phonemes[0].AutoEnvelope = true;
                    else { note.Phonemes[0].AutoEnvelope = false; note.Phonemes[0].Preutter = double.Parse(line.Trim().Replace("PreUtterance=", "")); }
                }
                if (line.StartsWith("VoiceOverlap=")) note.Phonemes[0].Overlap = double.Parse(line.Trim().Replace("VoiceOverlap=", ""));
                if (line.StartsWith("Envelope="))
                {
                    var pts = line.Trim().Replace("Envelope=", "").Split(new[] { ',' });
                    if (pts.Count() > 5) note.Expressions["release"].Data = 100 - (int)double.Parse(pts[5]);
                }
                if (line.StartsWith("VBR=")) VibratoFromUst(note.Vibrato, line.Trim().Replace("VBR=", ""));
                if (line.StartsWith("PBS=")) pbs = line.Trim().Replace("PBS=", "");
                if (line.StartsWith("PBW=")) pbw = line.Trim().Replace("PBW=", "");
                if (line.StartsWith("PBY=")) pby = line.Trim().Replace("PBY=", "");
                if (line.StartsWith("PBM=")) pbm = line.Trim().Replace("PBM=", "");
            }

            if (pbs != "")
            {
                var pts = note.PitchBend.Data as List<PitchPoint>;
                pts.Clear();
                // PBS
                if (pbs.Contains(';'))
                {
                    pts.Add(new PitchPoint(double.Parse(pbs.Split(new[] { ';' })[0]), double.Parse(pbs.Split(new[] { ';' })[1])));
                        note.PitchBend.SnapFirst = false;
                }
                else
                {
                    pts.Add(new PitchPoint(double.Parse(pbs), 0));
                    note.PitchBend.SnapFirst = true;
                }
                double x = pts.First().X;
                if (pbw != "")
                {
                    string[] w = pbw.Split(',');
                    string[] y = null;
                    if (w.Count() > 1) y = pby.Split(',');
                    for (int i = 0; i < w.Count() - 1; i++)
                    {
                        x += w[i] == "" ? 0 : float.Parse(w[i]);
                        pts.Add(new PitchPoint(x, y[i] == "" ? 0 : double.Parse(y[i])));
                    }
                    pts.Add(new PitchPoint(x + double.Parse(w[w.Count() - 1]), 0));
                }
                if (pbm != "")
                {
                    string[] m = pbw.Split(new[] { ',' });
                    for (int i = 0; i < m.Count() - 1; i++)
                    {
                        pts[i].Shape = m[i] == "r" ? PitchPointShape.Out :
                                       m[i] == "s" ? PitchPointShape.Linear :
                                       m[i] == "j" ? PitchPointShape.In : PitchPointShape.InOut;
                    }
                }
            }
            return note;
        }

        static List<string> NoteToUst(UNote note, UstVersion version) {
            var list = new List<string>();
            list.Add("Lyric=" + (note.Phonemes[0].AutoRemapped ? "" : "?") + note.Phonemes[0].Phoneme);
            list.Add("Length=" + note.DurTick);
            list.Add("NoteNum=" + note.NoteNum);
            list.Add("PreUtterance=" + (note.Phonemes[0].AutoEnvelope ? "" : note.Phonemes[0].Preutter.ToString()));
            if (note.Lyric == "R") return list;
            list.Add("Velocity=" + note.Expressions["velocity"].Data);
            list.Add("Intensity=" + note.Expressions["volume"].Data);
            list.Add("VoiceOverlap=" + note.Phonemes[0].Overlap);
            if (!note.Phonemes[0].AutoEnvelope || (int)note.Expressions["release"].Data > 0)
            {
                list.Add("Envelope=" + Math.Abs(note.Phonemes[0].Envelope.Points[1].X + note.Phonemes[0].Preutter) + "," + (note.Phonemes[0].Envelope.Points[2].X + note.Phonemes[0].Preutter + note.Phonemes[0].Overlap) + "," + Math.Abs(note.Phonemes[0].DurTick + note.Phonemes[0].TailOverlap - note.Phonemes[0].Envelope.Points[3].X) + "," + note.Phonemes[0].Envelope.Points[1].Y + "," + note.Phonemes[0].Envelope.Points[2].Y + "," + note.Phonemes[0].Envelope.Points[3].Y + "," + note.Phonemes[0].Envelope.Points[4].Y);
            }
            list.Add("VBR=" + VibratoToUst(note.Vibrato));
            if (note.PitchBend.Points.Any()) {
                list.Add("PBS=" + (note.PitchBend.SnapFirst ? note.PitchBend.Points[0].X.ToString() : note.PitchBend.Points[0].X + ";" + note.PitchBend.Points[0].Y));
                if (note.PitchBend.Points.Count > 1)
                {
                    var str1 = "";
                    var str2 = "";
                    for (int i = 1; i < note.PitchBend.Points.Count; i++)
                    {
                        if (i == note.PitchBend.Points.Count - 1)
                        {
                            str1 += note.PitchBend.Points[i].X;
                            str2 = string.IsNullOrEmpty(str2) ? str2 : str2.Remove(str2.Length - 1, 1);
                        }
                        else
                        {
                            str1 += note.PitchBend.Points[i].X + ",";
                            str2 += note.PitchBend.Points[i].Y + ",";
                        }
                    }
                    list.Add("PBW=" + str1);
                    if (!string.IsNullOrEmpty(str2)) list.Add("PBY=" + str2);
                }
                var str3 = "";
                for (int i = 0; i < note.PitchBend.Points.Count - 1; i++)
                {
                    var shape = note.PitchBend.Points[i].Shape;
                    str3 += (shape == PitchPointShape.Out ? "r" : shape == PitchPointShape.Linear ? "s" : shape == PitchPointShape.In ? "j" : "") + ",";
                }
                str3 = str3.Remove(str3.Length - 1, 1);
                list.Add("PBM=" + str3);
            }
            list.Add("Flags=" + note.GetResamplerFlags());
            return list;
        }

        static void VibratoFromUst(VibratoExpression vibrato, string ust)
        {
            var args = ust.Split(new[] { ',' }).Select(double.Parse).ToList();
            if (args.Count() >= 7)
            {
                vibrato.Length = args[0];
                vibrato.Period = args[1];
                vibrato.Depth = args[2];
                vibrato.In = args[3];
                vibrato.Out = args[4];
                vibrato.Shift = args[5];
                vibrato.Drift = args[6];
                vibrato.Enable();
            }
        }

        static String VibratoToUst(VibratoExpression vibrato)
        {
            List<double> args = new List<double>()
            {
                vibrato.Length,
                vibrato.Period,
                vibrato.Depth,
                vibrato.In,
                vibrato.Out,
                vibrato.Shift,
                vibrato.Drift
            };
            return string.Join(",", args.ToArray());
        }
    }
}
