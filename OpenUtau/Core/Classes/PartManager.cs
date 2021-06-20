using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using OpenUtau.Core.USTx;
using OpenUtau.Core.Render;

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

        public static void UpdatePart(UVoicePart part, bool commanded = false)
        {
            if (part == null || part.TrackNo < 0 || part.TrackNo >= DocManager.Inst.Project.Tracks.Count) return;
            var singer = DocManager.Inst.Project.Tracks[part.TrackNo].Singer;
            UpdatePart(part, singer, commanded: commanded);
        }

        public static void RenewPartNo() {
            int pno = 0;
            foreach (var part in DocManager.Inst.Project.Parts.OrderBy(part=>part.TrackNo).ThenBy(part=>part.PosTick))
            {
                part.PartNo = pno;
                if (part is UVoicePart voice) {
                    foreach (var note in voice.Notes)
                    {
                        note.PartNo = pno;
                    }
                }
                pno++;
            }
            RenderCache.Inst.Clear();
            RenderDispatcher.Inst.trackCache.ForEach(pair => pair.Baked?.Close());
            RenderDispatcher.Inst.trackCache.Clear();
            RenderDispatcher.Inst.ReleasePartCache();
        }

        public static void UpdatePart(UVoicePart part, USinger singer, bool shouldRedraw = true, bool commanded = false)
        {
            lock (part)
            {
                if (part == null) return;
                CheckOverlappedNotes(part);
                if(commanded)UpdatePhonemes(part, singer);
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
            double ClampIntoEnvelope(EnvelopeExpression envelope, ExpPoint point, double posTick) {
                var pt1 = envelope.Points.FindAll(pt => pt.X <= (posTick + point.X)).OrderByDescending(a => a.X).ThenByDescending(a => a.Y).FirstOrDefault();
                if(pt1 == null) { pt1 = new ExpPoint(0, 0); }
                var pt2 = envelope.Points.Find(pt => pt.X > (point.X + posTick));
                if (pt2 == null) return Math.Min(pt1.Y, point.Y);
                return Math.Min((pt1.Y - pt2.Y) / (pt1.X - pt2.X) * (point.X - pt1.X + posTick) + pt1.Y, point.Y);
            }

            foreach (UNote note in part.Notes)
            {
                foreach (UPhoneme phoneme in note.Phonemes)
                {
                    phoneme.Envelope.Points[0].X = -phoneme.Preutter;
                    phoneme.Envelope.Points[0].Y = 0;
                    phoneme.Envelope.Points[1].X = phoneme.Envelope.Points[0].X + (phoneme.Overlapped ? phoneme.Overlap : 5);
                    phoneme.Envelope.Points[3].X = DocManager.Inst.Project.TickToMillisecond(phoneme.DurTick, part.PosTick + note.PosTick + phoneme.PosTick) - phoneme.TailIntrude;
                    phoneme.Envelope.Points[2].X = Math.Min(Math.Max(0, phoneme.Envelope.Points[1].X), phoneme.Envelope.Points[3].X);
                    phoneme.Envelope.Points[4].X = phoneme.Envelope.Points[3].X + phoneme.TailOverlap;
                    phoneme.Envelope.Points[4].Y = 0;

                    phoneme.Envelope.Points[1].Y = (int)phoneme.Parent.Expressions["volume"].Data;
                    if (phoneme.PosTick == 0)
                    {
                        phoneme.Envelope.Points[1].X = phoneme.Envelope.Points[0].X + (phoneme.Overlapped ? phoneme.Overlap : 5) * (int)phoneme.Parent.Expressions["accent"].Data / 100.0;
                        phoneme.Envelope.Points[1].Y = (int)phoneme.Parent.Expressions["accent"].Data * (int)phoneme.Parent.Expressions["volume"].Data / 100;
                    }
                    phoneme.Envelope.Points[2].X = Math.Min(Math.Max(0, phoneme.Envelope.Points[1].X), phoneme.Envelope.Points[3].X);
                    phoneme.Envelope.Points[2].Y = (int)phoneme.Parent.Expressions["volume"].Data;
                    phoneme.Envelope.Points[3].Y = (int)phoneme.Parent.Expressions["volume"].Data;
                    phoneme.Envelope.Points[3].X -= (phoneme.Envelope.Points[3].X - phoneme.Envelope.Points[2].X) * (int)phoneme.Parent.Expressions["release"].Data / 500;
                    //phoneme.Envelope.Points[3].Y *= 1.0 - (int)phoneme.Parent.Expressions["release"].Data / 100.0;
                    phoneme.Envelope.Points[2].X += (phoneme.Envelope.Points[3].X - phoneme.Envelope.Points[2].X) * ((int)phoneme.Parent.Expressions["decay"].Data / 100.0);
                }
                note.Envelope.Points[0] = note.Phonemes.First().Envelope.Points[0].Clone();
                note.Envelope.Points[1] = note.Phonemes.First().Envelope.Points[1].Clone();
                var lastpho = note.Phonemes.Last();
                note.Envelope.Points[3] = lastpho.Envelope.Points[3].Clone();
                note.Envelope.Points[4] = lastpho.Envelope.Points[4].Clone();
                var posCompansate = DocManager.Inst.Project.TickToMillisecond(lastpho.PosTick, part.PosTick + note.PosTick);
                note.Envelope.Points[3].X += posCompansate;
                note.Envelope.Points[4].X += posCompansate;

                note.Envelope.Points[1].Y = (int)note.Expressions["volume"].Data;
                note.Envelope.Points[1].X = note.Envelope.Points[0].X + (note.Phonemes.First().Overlapped ? note.Phonemes.First().Overlap : 5) * (int)note.Expressions["accent"].Data / 100.0;
                note.Envelope.Points[1].Y = (int)note.Expressions["accent"].Data * (int)note.Expressions["volume"].Data / 100;
                note.Envelope.Points[2].X = Math.Min(Math.Max(0, note.Envelope.Points[1].X), note.Envelope.Points[3].X);
                note.Envelope.Points[2].Y = (int)note.Expressions["volume"].Data;
                note.Envelope.Points[3].Y = (int)note.Expressions["volume"].Data;
                note.Envelope.Points[3].X -= (note.Envelope.Points[3].X - note.Envelope.Points[2].X) * (int)note.Expressions["release"].Data / 500;
                note.Envelope.Points[3].Y *= 1.0 - (int)note.Expressions["release"].Data / 100.0;
                note.Envelope.Points[2].X += (note.Envelope.Points[3].X - note.Envelope.Points[2].X) * ((int)note.Expressions["decay"].Data / 100.0);
                foreach (var item in note.Phonemes)
                {
                    foreach (var item1 in item.Envelope.Points)
                    {
                        item1.Y = ClampIntoEnvelope(note.Envelope, item1, DocManager.Inst.Project.TickToMillisecond(item.PosTick, part.PosTick + note.PosTick));
                    }
                }
            }
        }

        internal static void UpdateOverlapAdjustment(UVoicePart part) {
            UpdateOverlapAdjustment(part.Notes, part.PosTick);
        }

        internal static void UpdateOverlapAdjustment(IEnumerable<UNote> notes, int posTick)
        {
            UPhoneme lastPhoneme = null;
            UNote lastNote = null;
            foreach (UNote note in notes)
            {
                foreach (UPhoneme phoneme in note.Phonemes)
                {
                    if (lastPhoneme != null)
                    {
                        int gapTick = phoneme.Parent.PosTick + phoneme.PosTick - lastPhoneme.Parent.PosTick - lastPhoneme.EndTick;
                        double gapMs = DocManager.Inst.Project.TickToMillisecond(gapTick, posTick + note.PosTick + phoneme.PosTick);
                        if (gapMs < phoneme.Preutter)
                        {
                            phoneme.Overlapped = true;
                            double lastDurMs = DocManager.Inst.Project.TickToMillisecond(lastPhoneme.DurTick, posTick + lastPhoneme.Parent.PosTick + lastPhoneme.PosTick);
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

                            phoneme.Preutter = Math.Min(phoneme.Preutter, DocManager.Inst.Project.TickToMillisecond(lastPhoneme.DurTick, posTick + lastPhoneme.Parent.PosTick + lastPhoneme.PosTick));
                            phoneme.Overlap = Math.Min(phoneme.Overlap, DocManager.Inst.Project.TickToMillisecond(phoneme.DurTick, posTick + note.PosTick + phoneme.PosTick));

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
                    bool hyphenHolder = phoneme.Phoneme.Equals("-");
                    if (singer.AliasMap.ContainsKey(phoneme.PhonemeRemapped))
                    {
                        phoneme.Oto = singer.AliasMap[phoneme.PhonemeRemapped];
                        phoneme.PhonemeError = false;
                        phoneme.Overlap = phoneme.Oto.Overlap + DocManager.Inst.Project.TickToMillisecond(hyphenHolder ? Math.Min(oldpho.DurTick * 0.6, phoneme.DurTick) : 0);
                        phoneme.Preutter = phoneme.Oto.Preutter + DocManager.Inst.Project.TickToMillisecond(hyphenHolder ? Math.Min(oldpho.DurTick * 0.2, phoneme.DurTick) : 0);
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
                            note.Phonemes[i].PosTick = (int)Math.Round((float)presetpho[i].Parent.PosTick / totallen * note.DurTick);
                            note.Phonemes[i].DurTick = (int)Math.Round((float)presetpho[i].Parent.DurTick / totallen * note.DurTick);
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

        private static void UpdatePhonemes(UVoicePart part, USinger singer)
        {
            UNote GetFormerNote(UNote me) {
                return part.Notes.FirstOrDefault(note1 => note1 != me && Math.Abs(me.PosTick - note1.EndTick) < DocManager.Inst.Project.Resolution / 64);
            }
            string GetSuitableLyrics(UNote me) {
                if (me.Lyric != "-")
                {
                    return me.Lyric;
                }
                else {
                    UNote former = me;
                    do
                    {
                        former = GetFormerNote(former);
                    } while (former != null && former.Lyric == "-");
                    return former == null ? "-" : Util.LyricsHelper.GetVowel(former.Lyric, singer);
                }
            }
            if (part == null || singer == null) return;
            foreach (var note in part.Notes)
            {
                if (singer.PresetLyricsMap.ContainsKey(note.Lyric))
                {
                    var preset = singer.PresetLyricsMap[note.Lyric];
                    int totallen = preset.Notes.Sum(pair => pair.Value.DurTick);
                    if (note.Phonemes.Count != preset.Notes.Values.Sum(unote => unote.Phonemes.Count))
                        UDictionaryNote.ApplyPreset(note, preset);
                }
                else if (part.ConvertStyle ?? Util.Preferences.Default.AutoConvertStyles)
                {
                    UNote former = GetFormerNote(note);
                    UNote lator = part.Notes.FirstOrDefault(note1 => note1 != note && Math.Abs(note1.PosTick - note.EndTick) < DocManager.Inst.Project.Resolution / 64);
                    var lyrics = GetSuitableLyrics(note);
                    var mod = Util.SamplingStyleHelper.GetCorrespondingPhoneme(lyrics, former, lator, singer.Style, singer);
                    var mods = mod.Split('\t');
                    if (mods.Length == 1) {
                        if(note.Phonemes.Count == 1)
                            note.Phonemes[0].Phoneme = mods[0];
                        else
                        {
                            note.Phonemes.Clear();
                            note.Phonemes.Add(new UPhoneme { Phoneme = mods[0], Parent = note});
                        }
                    }
                    else
                    {
                        if (note.Phonemes.Count == 2)
                        {
                            note.Phonemes[0].Phoneme = mods[0];
                            note.Phonemes[1].Phoneme = mods[1];
                            note.Phonemes[1].PosTick = Math.Max((int)Math.Round(note.DurTick * 0.75), note.DurTick - 60);
                        }
                        else
                        {
                            note.Phonemes.Clear();
                            note.Phonemes.Add(new UPhoneme() { Phoneme = mods[0], Parent = note });
                            note.Phonemes.Add(new UPhoneme() { Phoneme = mods[1], Parent = note, PosTick = Math.Max((int)Math.Round(note.DurTick * 0.75), note.DurTick - 60) });
                        }
                    }
                }
                else
                {
                    UNote former = part.Notes.FirstOrDefault(note1 => note1 != note && Math.Abs(note.PosTick - note1.EndTick) < DocManager.Inst.Project.Resolution / 64);
                    var lyrics = GetSuitableLyrics(note);
                    if (note.Phonemes.Count == 1)
                        note.Phonemes[0].Phoneme = lyrics;
                    else
                    {
                        note.Phonemes.Clear();
                        note.Phonemes.Add(new UPhoneme() { Parent = note, Phoneme = lyrics });
                    }
                }
            }
        }

        private static void CheckOverlappedNotes(UVoicePart part)
        {
            foreach (UNote note in part.Notes)
            {
                var li = part.Notes.SkipWhile(note1=> (note1.PosTick <= note.PosTick && note1.EndTick <= note.PosTick) || (note1.PosTick >= note.EndTick));
                li = li.TakeWhile(note1 => note1.PosTick < note.EndTick);
                if (li.Count() > 1) {
                    note.Error = true;
                    li.ToList().ForEach(note1 => note1.Error = true);
                }
                else
                {
                    note.Error = false;
                }
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

        public void PostOnNext(UCommandGroup cmds, bool isUndo) { }

        #endregion

    }
}
