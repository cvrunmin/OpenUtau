using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Midi;

using OpenUtau.Core.USTx;

namespace OpenUtau.Core.Formats
{
    public static class Midi
    {
        static public List<UVoicePart> Load(string file, UProject project)
        {
            List<UVoicePart> resultParts = new List<UVoicePart>();
            MidiFile midi = new MidiFile(file);
            for (int i = 0; i < midi.Tracks; i++)
            {
                Dictionary<int, UVoicePart> parts = new Dictionary<int, UVoicePart>();
                foreach (var e in midi.Events.GetTrackEvents(i))
                    if (e is NoteOnEvent)
                    {
                        var _e = e as NoteOnEvent;
                        if (!parts.ContainsKey(_e.Channel)) parts.Add(_e.Channel, new UVoicePart());
                        var note = project.CreateNote(
                            _e.NoteNumber,
                            (int)_e.AbsoluteTime * project.Resolution / midi.DeltaTicksPerQuarterNote,
                            _e.NoteLength * project.Resolution / midi.DeltaTicksPerQuarterNote);
                        parts[e.Channel].Notes.Add(note);
                    }
                foreach (var pair in parts)
                {
                    pair.Value.DurTick = pair.Value.GetMinDurTick(project);
                    resultParts.Add(pair.Value);
                }
            }
            return resultParts;
        }

        public static void Save(string pathFormat, UProject project, bool exportTrack)
        {
            var parts = project.Parts.OfType<UVoicePart>();
            if (exportTrack)
            {
                foreach (var track in project.Tracks)
                {
                    var tParts = parts.Where(part => part.TrackNo == track.TrackNo).OrderBy(part => part.PosTick);
                    if (tParts.Count() == 0) continue;
                    var midi = new MidiEventCollection(1, project.Resolution);
                    var controlTrack = midi.AddTrack();
                    controlTrack.Add(new TextEvent("Control", MetaEventType.SequenceTrackName, 0));
                    controlTrack.Add(new TempoEvent((int)Math.Round(60000000 / project.BPM), 0));
                    controlTrack.Add(new TimeSignatureEvent(0, 4, 4, 24, 8));
                    controlTrack.Add(new TextEvent("Setup", MetaEventType.Marker, 0));
                    controlTrack.Add(new TextEvent("Settings", MetaEventType.TextEvent, 0));
                    controlTrack.Add(new TextEvent("@rem project=" + project.Name + "-" + track.Name, MetaEventType.TextEvent, 0));
                    controlTrack.Add(new TextEvent("@set tempo=" + project.BPM, MetaEventType.TextEvent, 0));
                    //controlTrack.Add(new MetaEvent(MetaEventType.EndTrack, 0, 0));
                    var noteTrack = midi.AddTrack();
                    noteTrack.Add(new TextEvent("Voice A", MetaEventType.SequenceTrackName, 0));
                    noteTrack.Add(new TextEvent("This file format is subject to modify or mark obsolete without notified.", MetaEventType.TextEvent, 0));
                    noteTrack.Add(new ControlChangeEvent(0, 1, (MidiController)101, 0));
                    noteTrack.Add(new ControlChangeEvent(0, 1, (MidiController)100, 0));
                    noteTrack.Add(new ControlChangeEvent(0, 1, (MidiController)6, 0));
                    noteTrack.Add(new ControlChangeEvent(0, 1, (MidiController)38, 0));
                    var counter = 1;
                    foreach (var part in tParts)
                    {
                        foreach (var note in part.Notes)
                        {
                            var absTime = part.PosTick + note.PosTick;
                            string text = string.Format("{0:0000}: flag={1} env={2} VBR={3}",
                                counter,
                                note.GetResamplerFlags(),
                                Math.Abs(note.Phonemes[0].Envelope.Points[0].X + note.Phonemes[0].Preutter)
                                + ","
                                + (note.Phonemes[0].Envelope.Points[1].X - note.Phonemes[0].Envelope.Points[0].X)
                                + ","
                                + Math.Abs(note.Phonemes[0].Envelope.Points[4].X - note.Phonemes[0].Envelope.Points[3].X)
                                + ","
                                + note.Phonemes[0].Envelope.Points[0].Y + "," + note.Phonemes[0].Envelope.Points[1].Y + "," + note.Phonemes[0].Envelope.Points[3].Y + "," + note.Phonemes[0].Envelope.Points[4].Y + ",%,0," + Math.Min(Math.Max(0, note.Phonemes[0].Envelope.Points[2].X - note.Phonemes[0].Envelope.Points[1].X), note.Phonemes[0].Envelope.Points[3].X - note.Phonemes[0].Envelope.Points[2].X) + "," + note.Phonemes[0].Envelope.Points[2].Y,
                                Ust.VibratoToUst(note.Vibrato)
                                );
                            if (note.PitchBend.Points.Any())
                            {
                                string.Concat(text, " PBS=" + (note.PitchBend.SnapFirst ? note.PitchBend.Points[0].X.ToString() : note.PitchBend.Points[0].X + ";" + note.PitchBend.Points[0].Y));
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
                                    string.Concat(text, " PBW=", str1);
                                    if (!string.IsNullOrEmpty(str2)) string.Concat(text, " PBY=", str2);
                                }
                                var str3 = "";
                                for (int i = 0; i < note.PitchBend.Points.Count - 1; i++)
                                {
                                    var shape = note.PitchBend.Points[i].Shape;
                                    str3 += (shape == PitchPointShape.Out ? "r" : shape == PitchPointShape.Linear ? "s" : shape == PitchPointShape.In ? "j" : "") + ",";
                                }
                                if (str3.Length > 0)
                                    str3 = str3.Remove(str3.Length - 1, 1);
                                string.Concat(text, " PBM=" + str3);
                            }
                            noteTrack.Add(new TextEvent(text, MetaEventType.TextEvent, absTime));
                            noteTrack.Add(new TextEvent(note.Lyric, MetaEventType.Lyric, absTime));
                            noteTrack.Add(new NoteOnEvent(absTime, 1, note.NoteNum, note.Expressions.ContainsKey("volume") ? (int)note.Expressions["volume"].Data : 100, note.DurTick));
                            noteTrack.Add(new NoteEvent(absTime + note.DurTick, 1, MidiCommandCode.NoteOff, note.NoteNum, 0));
                        }
                    }
                    //controlTrack.Add(new MetaEvent(MetaEventType.EndTrack, 0, tParts.Last().EndTick));
                    midi.PrepareForExport();
                    MidiFile.Export($"{pathFormat}_Track-{track.TrackNo}.mid", midi);
                }
            }
            else
            {
                foreach (var part in parts)
                {
                    var midi = new MidiEventCollection(1, project.Resolution);
                    var controlTrack = midi.AddTrack();
                    controlTrack.Add(new TextEvent("Control", MetaEventType.SequenceTrackName, 0));
                    controlTrack.Add(new TempoEvent((int)Math.Round(60000000 / project.BPM), 0));
                    controlTrack.Add(new TimeSignatureEvent(0, 4, 4, 24, 8));
                    controlTrack.Add(new TextEvent("Setup", MetaEventType.Marker, 0));
                    controlTrack.Add(new TextEvent("Settings", MetaEventType.TextEvent, 0));
                    controlTrack.Add(new TextEvent("@rem project=" + project.Name + "-" + part.Name, MetaEventType.TextEvent, 0));
                    controlTrack.Add(new TextEvent("@set tempo=" + project.BPM, MetaEventType.TextEvent, 0));
                    //controlTrack.Add(new MetaEvent(MetaEventType.EndTrack, 0, 0));
                    var noteTrack = midi.AddTrack();
                    noteTrack.Add(new TextEvent("Voice A", MetaEventType.SequenceTrackName, 0));
                    noteTrack.Add(new TextEvent("This file format is subject to modify or mark obsolete without notified.", MetaEventType.TextEvent, 0));
                    noteTrack.Add(new ControlChangeEvent(0, 1, (MidiController)101, 0));
                    noteTrack.Add(new ControlChangeEvent(0, 1, (MidiController)100, 0));
                    noteTrack.Add(new ControlChangeEvent(0, 1, (MidiController)6, 0));
                    noteTrack.Add(new ControlChangeEvent(0, 1, (MidiController)38, 0));
                    var counter = 1;
                    foreach (var note in part.Notes)
                    {
                        var absTime = note.PosTick;
                        string text = string.Format("{0:0000}: flag={1} env={2} VBR={3}",
                            counter,
                            note.GetResamplerFlags(),
                            Math.Abs(note.Phonemes[0].Envelope.Points[0].X + note.Phonemes[0].Preutter)
                            + ","
                            + (note.Phonemes[0].Envelope.Points[1].X - note.Phonemes[0].Envelope.Points[0].X)
                            + ","
                            + Math.Abs(note.Phonemes[0].Envelope.Points[4].X - note.Phonemes[0].Envelope.Points[3].X)
                            + ","
                            + note.Phonemes[0].Envelope.Points[0].Y + "," + note.Phonemes[0].Envelope.Points[1].Y + "," + note.Phonemes[0].Envelope.Points[3].Y + "," + note.Phonemes[0].Envelope.Points[4].Y + ",%,0," + Math.Min(Math.Max(0, note.Phonemes[0].Envelope.Points[2].X - note.Phonemes[0].Envelope.Points[1].X), note.Phonemes[0].Envelope.Points[3].X - note.Phonemes[0].Envelope.Points[2].X) + "," + note.Phonemes[0].Envelope.Points[2].Y,
                            Ust.VibratoToUst(note.Vibrato)
                            );
                        if (note.PitchBend.Points.Any())
                        {
                            string.Concat(text, " PBS=" + (note.PitchBend.SnapFirst ? note.PitchBend.Points[0].X.ToString() : note.PitchBend.Points[0].X + ";" + note.PitchBend.Points[0].Y));
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
                                string.Concat(text, " PBW=", str1);
                                if (!string.IsNullOrEmpty(str2)) string.Concat(text, " PBY=", str2);
                            }
                            var str3 = "";
                            for (int i = 0; i < note.PitchBend.Points.Count - 1; i++)
                            {
                                var shape = note.PitchBend.Points[i].Shape;
                                str3 += (shape == PitchPointShape.Out ? "r" : shape == PitchPointShape.Linear ? "s" : shape == PitchPointShape.In ? "j" : "") + ",";
                            }
                            if (str3.Length > 0)
                                str3 = str3.Remove(str3.Length - 1, 1);
                            string.Concat(text, " PBM=" + str3);
                        }
                        noteTrack.Add(new TextEvent(text, MetaEventType.TextEvent, absTime));
                        noteTrack.Add(new TextEvent(note.Lyric, MetaEventType.Lyric, absTime));
                        noteTrack.Add(new NoteOnEvent(absTime, 1, note.NoteNum, note.Expressions.ContainsKey("volume") ? (int)note.Expressions["volume"].Data : 100, note.DurTick));
                        noteTrack.Add(new NoteEvent(absTime + note.DurTick, 1, MidiCommandCode.NoteOff, note.NoteNum, 0));
                    }
                    //controlTrack.Add(new MetaEvent(MetaEventType.EndTrack, 0, part.EndTick));
                    midi.PrepareForExport();
                    MidiFile.Export($"{pathFormat}_Track-{part.TrackNo}_Part-{part.PartNo}.mid", midi);
                }
            }
        }
    }
}
