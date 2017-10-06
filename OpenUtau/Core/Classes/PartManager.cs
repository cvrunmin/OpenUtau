using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using OpenUtau.Core.USTx;

namespace OpenUtau.Core
{
    public class PartManager : ICmdSubscriber
    {
        class PartContainer
        {
            public UVoicePart Part = null;
        }

        Timer timer;

        UProject _project;
        PartContainer _partContainer;

        public PartManager()
        {
            _partContainer = new PartContainer();
            this.Subscribe(DocManager.Inst);
            timer = new Timer(Update, _partContainer, 0, 100);
        }

        private void Update(Object state)
        {
            var partContainer = state as PartContainer;
            if (partContainer.Part == null) return;
            UpdatePart(partContainer.Part);
        }

        public static void UpdatePart(UVoicePart part)
        {
            if (part == null || part.TrackNo < 0 || part.TrackNo >= DocManager.Inst.Project.Tracks.Count) return;
            var singer = DocManager.Inst.Project.Tracks[part.TrackNo].Singer;
            UpdatePart(part, singer);
        }

        public static void UpdatePart(UVoicePart part, USinger singer, bool shouldRedraw = true)
        {
            lock (part)
            {
                if (part == null) return;
                CheckOverlappedNotes(part);
                UpdatePhonemeDurTick(part, singer);
                UpdatePhonemeOto(part, singer);
                UpdateOverlapAdjustment(part);
                UpdateEnvelope(part);
                UpdatePitchBend(part);
                if(shouldRedraw) DocManager.Inst.ExecuteCmd(new RedrawNotesNotification(), true);
            }
        }

        private static void UpdatePitchBend(UVoicePart part)
        {
            UNote lastNote = null;
            foreach (UNote note in part.Notes)
            {
                if (note.PitchBend.SnapFirst)
                {
                    if (note.Phonemes.Count > 0 && lastNote != null && (note.Phonemes[0].Overlapped || note.PosTick == lastNote.EndTick))
                        note.PitchBend.Points[0].Y = (lastNote.NoteNum - note.NoteNum) * 10;
                    else
                        note.PitchBend.Points[0].Y = 0;
                }
                lastNote = note;
            }
        }

        public static void ResnapPitchBend(UVoicePart part)
        {
            UNote lastNote = null;
            foreach (UNote note in part.Notes)
            {
                if (!note.PitchBend.SnapFirst)
                {
                    if (note.Phonemes.Count > 0 && note.Phonemes[0].Overlapped && lastNote != null)
                        if (note.PitchBend.Points[0].Y == (lastNote.NoteNum - note.NoteNum) * 10)
                            note.PitchBend.SnapFirst = true;
                }
                lastNote = note;
            }
        }

        private static void UpdateEnvelope(UVoicePart part)
        {
            foreach (UNote note in part.Notes)
            {
                foreach (UPhoneme phoneme in note.Phonemes)
                {
                    phoneme.Envelope.Points[0].X = -phoneme.Preutter;
                    phoneme.Envelope.Points[1].X = phoneme.Envelope.Points[0].X + (phoneme.Overlapped ? phoneme.Overlap : 5);
                    phoneme.Envelope.Points[3].X = DocManager.Inst.Project.TickToMillisecond(phoneme.DurTick, part.PosTick + note.PosTick + phoneme.PosTick) - phoneme.TailIntrude;
                    phoneme.Envelope.Points[2].X = Math.Min(Math.Max(0, phoneme.Envelope.Points[1].X), phoneme.Envelope.Points[3].X);
                    phoneme.Envelope.Points[4].X = phoneme.Envelope.Points[3].X + phoneme.TailOverlap;

                    phoneme.Envelope.Points[1].Y = (int)phoneme.Parent.Expressions["volume"].Data;
                    phoneme.Envelope.Points[1].X = phoneme.Envelope.Points[0].X + (phoneme.Overlapped ? phoneme.Overlap : 5) * (int)phoneme.Parent.Expressions["accent"].Data / 100.0;
                    phoneme.Envelope.Points[1].Y = (int)phoneme.Parent.Expressions["accent"].Data * (int)phoneme.Parent.Expressions["volume"].Data / 100;
                    phoneme.Envelope.Points[2].X = Math.Min(Math.Max(0, phoneme.Envelope.Points[1].X), phoneme.Envelope.Points[3].X);
                    phoneme.Envelope.Points[2].Y = (int)phoneme.Parent.Expressions["volume"].Data;
                    phoneme.Envelope.Points[3].Y = (int)phoneme.Parent.Expressions["volume"].Data;
                    phoneme.Envelope.Points[3].X -= (phoneme.Envelope.Points[3].X - phoneme.Envelope.Points[2].X) * (int)phoneme.Parent.Expressions["release"].Data / 500;
                    phoneme.Envelope.Points[3].Y *= 1.0 - (int)phoneme.Parent.Expressions["release"].Data / 100.0;
                    phoneme.Envelope.Points[2].X += (phoneme.Envelope.Points[3].X - phoneme.Envelope.Points[2].X) * ((int)phoneme.Parent.Expressions["decay"].Data / 100.0);
                }
            }
        }

        private static void UpdateOverlapAdjustment(UVoicePart part)
        {
            UPhoneme lastPhoneme = null;
            UNote lastNote = null;
            foreach (UNote note in part.Notes)
            {
                foreach (UPhoneme phoneme in note.Phonemes)
                {
                    if (lastPhoneme != null)
                    {
                        int gapTick = phoneme.Parent.PosTick + phoneme.PosTick - lastPhoneme.Parent.PosTick - lastPhoneme.EndTick;
                        double gapMs = DocManager.Inst.Project.TickToMillisecond(gapTick, part.PosTick + note.PosTick + phoneme.PosTick);
                        if (gapMs < phoneme.Preutter)
                        {
                            phoneme.Overlapped = true;
                            double lastDurMs = DocManager.Inst.Project.TickToMillisecond(lastPhoneme.DurTick, part.PosTick + lastPhoneme.Parent.PosTick + lastPhoneme.PosTick);
                            double correctionRatio = (lastDurMs + Math.Min(0, gapMs)) / 2 / (phoneme.Preutter - phoneme.Overlap);
                            if (phoneme.Preutter - phoneme.Overlap > gapMs + lastDurMs / 2)
                            {
                                phoneme.OverlapCorrection = true;
                                phoneme.Preutter = gapMs + (phoneme.Preutter - gapMs) * correctionRatio;
                                phoneme.Overlap *= correctionRatio;
                            }
                            else if (phoneme.Preutter > gapMs + lastDurMs)
                            {
                                phoneme.OverlapCorrection = true;
                                phoneme.Overlap *= correctionRatio; 
                                phoneme.Preutter = gapMs + lastDurMs;
                            }
                            else phoneme.OverlapCorrection = false;

                            phoneme.Preutter = Math.Min(phoneme.Preutter, DocManager.Inst.Project.TickToMillisecond(lastPhoneme.DurTick, part.PosTick + lastPhoneme.Parent.PosTick + lastPhoneme.PosTick));
                            phoneme.Overlap = Math.Min(phoneme.Overlap, DocManager.Inst.Project.TickToMillisecond(phoneme.DurTick, part.PosTick + note.PosTick + phoneme.PosTick));

                            lastPhoneme.TailIntrude = phoneme.Preutter - gapMs;
                            lastPhoneme.TailOverlap = phoneme.Overlap;

                        }
                        else
                        {
                            phoneme.Overlapped = false;
                            lastPhoneme.TailIntrude = 0;
                            lastPhoneme.TailOverlap = 0;
                        }
                    }
                    else phoneme.Overlapped = false;
                    lastPhoneme = phoneme;
                }
                lastNote = note;
            }
        }

        private static void UpdatePhonemeOto(UVoicePart part, USinger singer)
        {
            if (singer == null || !singer.Loaded) return;
            UPhoneme oldpho = null;
            foreach (UNote note in part.Notes)
            {
                foreach (UPhoneme phoneme in note.Phonemes)
                {
                    if (phoneme.AutoRemapped)
                    {
                        if (phoneme.Phoneme.StartsWith("?"))
                        {
                            phoneme.Phoneme = phoneme.Phoneme.Substring(1);
                            phoneme.AutoRemapped = false;
                        }
                        else
                        {
                            string noteString = MusicMath.GetNoteString(note.NoteNum);
                            if (singer.PitchMap.ContainsKey(noteString))
                            {
                                phoneme.RemappedBank = singer.PitchMap[noteString];
                            }
                            else
                            {
                                phoneme.RemappedBank = "";
                            }
                        }
                    }
                    bool hyphenHolder = note.Lyric.Equals("-") || phoneme.Phoneme.Equals("-");
                    if (phoneme.Phoneme.Equals("-")) {
                        try
                        {
                            phoneme.Phoneme = Util.LyricsHelper.GetVowel(oldpho.Phoneme);
                        }
                        catch (Exception)
                        {
                            System.Diagnostics.Debug.WriteLine("Cannot replace \"-\" placeholder");
                        }
                    }

                    if (singer.AliasMap.ContainsKey(phoneme.PhonemeRemapped))
                    {
                        phoneme.Oto = singer.AliasMap[phoneme.PhonemeRemapped];
                        phoneme.PhonemeError = false;
                        phoneme.Overlap = phoneme.Oto.Overlap + (hyphenHolder ? Math.Min(oldpho.DurTick * 0.6, phoneme.DurTick) : 0);
                        phoneme.Preutter = phoneme.Oto.Preutter + (hyphenHolder ? Math.Min(oldpho.DurTick * 0.2, phoneme.DurTick) : 0);
                        int vel = (int)phoneme.Parent.Expressions["velocity"].Data;
                        if (vel != 100)
                        {
                            double stretchRatio = Math.Pow(2, 1.0 - (double)vel / 100);
                            phoneme.Overlap *= stretchRatio;
                            phoneme.Preutter *= stretchRatio;
                        }
                    }
                    else
                    {
                        phoneme.PhonemeError = true;
                        phoneme.Overlap = 0;
                        phoneme.Preutter = 0;
                    }
                    oldpho = phoneme;
                }
            }
        }

        private static void UpdatePhonemeDurTick(UVoicePart part, USinger singer)
        {
            if (part == null || singer == null) return;
            UNote lastNote = null;
            UPhoneme lastPhoneme = null;
            foreach (UNote note in part.Notes)
            {
                if (note.ApplyingPreset && singer.PresetLyricsMap.ContainsKey(note.Lyric))
                {
                    var preset = singer.PresetLyricsMap[note.Lyric];
                    int totallen = preset.Notes.Sum(pair => pair.Value.DurTick);
                    if (note.Phonemes.Count != preset.Notes.Values.Sum(unote => unote.Phonemes.Count))
                        UDictionaryNote.ApplyPreset(note, preset);
                    else
                    {
                        var presetpho = preset.Notes.Values.SelectMany(unote => unote.Phonemes).ToList();
                        for (int i = 0; i < note.Phonemes.Count && i < presetpho.Count; i++)
                        {
                            note.Phonemes[i].DurTick = (int)Math.Round((float)presetpho[i].DurTick / totallen * note.DurTick);
                        }
                        
                    }
                }
                else
                {
                    foreach (UPhoneme phoneme in note.Phonemes)
                    {
                        phoneme.DurTick = phoneme.Parent.DurTick - phoneme.PosTick;
                        if (lastPhoneme != null)
                            if (lastPhoneme.Parent == phoneme.Parent)
                                lastPhoneme.DurTick = phoneme.PosTick - lastPhoneme.PosTick;
                        lastPhoneme = phoneme;
                    }
                }
                lastNote = note;
            }
        }

        private static void CheckOverlappedNotes(UVoicePart part)
        {
            //UNote lastNote = null;
            foreach (UNote note in part.Notes)
            {
                var li = part.Notes.SkipWhile(note1=>note1.PosTick < note.PosTick && note1.EndTick <= note.PosTick).TakeWhile(note1 => note1.PosTick < note.EndTick);
                if (li.Count() > 1) {
                    note.Error = true;
                    li.ToList().ForEach(note1 => note1.Error = true);
                }
                else
                {
                    note.Error = false;
                }
                /*
                if (lastNote != null && lastNote.EndTick > note.PosTick)
                {
                    lastNote.Error = true;
                    note.Error = true;
                }
                else note.Error = false;
                lastNote = note;*/
            }
        }

        # region Cmd Handling

        private void OnProjectLoad(UNotification cmd)
        {
            foreach (UPart part in cmd.project.Parts)
                if (part is UVoicePart)
                    UpdatePart((UVoicePart)part);
        }

        # endregion

        # region ICmdSubscriber

        public void Subscribe(ICmdPublisher publisher) { if (publisher != null) publisher.Subscribe(this); }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is PartCommand)
            {
                var _cmd = cmd as PartCommand;
                if (_cmd.part != _partContainer.Part) return;
                else if (_cmd is RemovePartCommand) _partContainer.Part = null;
            }
            else if (cmd is UNotification)
            {
                var _cmd = cmd as UNotification;
                if (_cmd is LoadPartNotification) { if (!(_cmd.part is UVoicePart)) return; _partContainer.Part = (UVoicePart)_cmd.part; _project = _cmd.project; }
                else if (_cmd is LoadProjectNotification) OnProjectLoad(_cmd);
            }
        }

        # endregion

    }
}
