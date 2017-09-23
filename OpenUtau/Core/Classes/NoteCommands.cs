using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;

namespace OpenUtau.Core
{
    public abstract class NoteCommand : UCommand 
    {
        protected UNote[] Notes;
        public UVoicePart Part;
        public override void Execute()
        {
            if (Util.Preferences.Default.RenderNoteAtInstant)
            {
                foreach (var note in Notes)
                {
                    Render.ResamplerInterface.RenderNote(DocManager.Inst.Project, Part, note);
                }
            }
            if(Part != null && Part.TrackNo >= 0 && Part.TrackNo < DocManager.Inst.Project.Tracks.Count)
                DocManager.Inst.Project.Tracks[Part.TrackNo].Amended = true;
        }
        public override void Unexecute()
        {
            if (Part != null && Part.TrackNo >= 0 && Part.TrackNo < DocManager.Inst.Project.Tracks.Count)
                DocManager.Inst.Project.Tracks[Part.TrackNo].Amended = true;
        }
    }

    public class AddNoteCommand : NoteCommand
    {
        public AddNoteCommand(UVoicePart part, UNote note) { this.Part = part; this.Notes = new UNote[] { note }; }
        public AddNoteCommand(UVoicePart part, List<UNote> notes) { this.Part = part; this.Notes = notes.ToArray(); }
        public override string ToString() { return "Add note"; }
        public override void Execute()
        {
            lock (Part)
            {
                foreach (var note in Notes) Part.Notes.Add(note);
            }
            var ordered = Part.Notes.OrderBy(note => note.PosTick);
            for (int i = 0; i < Part.Notes.Count; i++)
            {
                 ordered.ElementAt(i).NoteNo = i;
            }
            base.Execute();
        }
        public override void Unexecute()
        {
            lock (Part)
            {
                foreach (var note in Notes) Part.Notes.Remove(note);
            }
            var ordered = Part.Notes.OrderBy(note => note.PosTick);
            for (int i = 0; i < Part.Notes.Count; i++)
            {
                ordered.ElementAt(i).NoteNo = i;
            }
            base.Unexecute();
        }
    }

    public class RemoveNoteCommand : NoteCommand
    {
        public RemoveNoteCommand(UVoicePart part, UNote note) { this.Part = part; this.Notes = new UNote[] { note }; }
        public RemoveNoteCommand(UVoicePart part, List<UNote> notes) { this.Part = part; this.Notes = notes.ToArray(); }
        public override string ToString() { return "Remove note"; }
        public override void Execute()
        {
            lock (Part)
            {
                foreach (var note in Notes)
                {
                    Part.Notes.Remove(note);
                }

                var ordered = Part.Notes.OrderBy(note => note.PosTick);
                for (int i = 0; i < Part.Notes.Count; i++)
                {
                    ordered.ElementAt(i).NoteNo = i;
                }
            }
            base.Execute();
        }
        public override void Unexecute()
        {
            lock (Part)
            {
                foreach (var note in Notes)
                {
                    Part.Notes.Add(note);
                }
                var ordered = Part.Notes.OrderBy(note => note.PosTick);
                for (int i = 0; i < Part.Notes.Count; i++)
                {
                    ordered.ElementAt(i).NoteNo = i;
                }
            }
            base.Unexecute();
        }
    }

    public class MoveNoteCommand : NoteCommand
    {
        int DeltaPos, DeltaNoteNum;
        public MoveNoteCommand(UVoicePart part, List<UNote> notes, int deltaPos, int deltaNoteNum)
        {
            this.Part = part;
            this.Notes = notes.ToArray();
            this.DeltaPos = deltaPos;
            this.DeltaNoteNum = deltaNoteNum;
        }
        public MoveNoteCommand(UVoicePart part, UNote note, int deltaPos, int deltaNoteNum)
        {
            this.Part = part;
            this.Notes = new UNote[] { note };
            this.DeltaPos = deltaPos;
            this.DeltaNoteNum = deltaNoteNum;
        }
        public override string ToString() { return string.Format("Move {0} notes", Notes.Count()); }
        public override void Execute() {
            lock (Part)
            {
                foreach (UNote note in Notes)
                {
                    Part.Notes.Remove(note);
                    note.PosTick += DeltaPos;
                    note.NoteNum += DeltaNoteNum;
                    Part.Notes.Add(note);
                }
                var ordered = Part.Notes.OrderBy(note => note.PosTick);
                for (int i = 0; i < Part.Notes.Count; i++)
                {
                    ordered.ElementAt(i).NoteNo = i;
                }
            }
            base.Execute();
        }
        public override void Unexecute()
        {
            lock (Part)
            {
                foreach (UNote note in Notes)
                {
                    Part.Notes.Remove(note);
                    note.PosTick -= DeltaPos;
                    note.NoteNum -= DeltaNoteNum;
                    Part.Notes.Add(note);
                }
                var ordered = Part.Notes.OrderBy(note => note.PosTick);
                for (int i = 0; i < Part.Notes.Count; i++)
                {
                    ordered.ElementAt(i).NoteNo = i;
                }
            }
            base.Unexecute();
        }
    }

    public class ResizeNoteCommand : NoteCommand
    {
        int DeltaDur;
        public ResizeNoteCommand(UVoicePart part, List<UNote> notes, int deltaDur) { this.Part = part; this.Notes = notes.ToArray(); this.DeltaDur = deltaDur; }
        public ResizeNoteCommand(UVoicePart part, UNote note, int deltaDur) { this.Part = part; this.Notes = new UNote[] { note }; this.DeltaDur = deltaDur; }
        public override string ToString() { return string.Format("Change {0} notes duration", Notes.Count()); }
        public override void Execute() { lock (Part) { foreach (var note in Notes) note.DurTick += DeltaDur; }base.Execute(); }
        public override void Unexecute() { lock (Part) { foreach (var note in Notes) note.DurTick -= DeltaDur; } base.Unexecute(); }
    }

    public class ChangeNoteLyricCommand : NoteCommand
    {
        string NewLyric, OldLyric;
        public ChangeNoteLyricCommand(UVoicePart part, UNote note, string newLyric) { this.Part = part; this.Notes = new []{ note }; this.NewLyric = newLyric; this.OldLyric = note.Lyric; }
        public override string ToString() { return "Change notes lyric"; }
        public override void Execute()
        {
            if (string.IsNullOrEmpty(NewLyric)) return;
            if(Part != null)
            lock (Part)
            {
                foreach (var Note in Notes)
                {
                    Note.Lyric = NewLyric;
                    var presetLyricsMap = DocManager.Inst.Project.Tracks[Part.TrackNo].Singer?.PresetLyricsMap;
                    if (presetLyricsMap != null && presetLyricsMap.ContainsKey(Note.Lyric))
                    {
                        UDictionaryNote.ApplyPreset(Note, presetLyricsMap[Note.Lyric]);
                    }
                    else if (!Note.ApplyingPreset)
                    {
                            if (Note.Phonemes.Count > 1) {

                                Note.Phonemes.Clear();
                                Note.Phonemes.Add(new UPhoneme() { Parent = Note, PosTick = 0, Phoneme = Note.Lyric });
                            }
                        Note.Phonemes[0].Phoneme = Note.Lyric;
                    }
                    else
                    {
                        Note.Phonemes.Clear();
                        Note.Phonemes.Add(new UPhoneme() { Parent = Note, PosTick = 0, Phoneme = Note.Lyric });
                        Note.ApplyingPreset = false;
                    }
                }
            }
            base.Execute();
        }
        public override void Unexecute()
        {
            lock (Part)
            {
                foreach (var Note in Notes)
                {
                    Note.Lyric = OldLyric;
                    Note.Phonemes[0].Phoneme = OldLyric;
                    var presetLyricsMap = DocManager.Inst.Project.Tracks[Part.TrackNo].Singer.PresetLyricsMap;
                    if (presetLyricsMap.ContainsKey(Note.Lyric))
                    {
                        UDictionaryNote.ApplyPreset(Note, presetLyricsMap[Note.Lyric]);
                    }
                    else if (!Note.ApplyingPreset)
                    {
                        Note.Phonemes[0].Phoneme = Note.Lyric;
                    }
                    else
                    {
                        Note.Phonemes.Clear();
                        Note.Phonemes.Add(new UPhoneme() { Parent = Note, PosTick = 0, Phoneme = Note.Lyric });
                        Note.ApplyingPreset = false;
                    }
                }
            }
            base.Unexecute();
        }
    }
}
