using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.USTx
{
    /// <summary>
    /// Music note.
    /// </summary>
    public class UNote : IComparable
    {
        public int PosTick;
        public int DurTick;
        public int NoteNum;
        public int NoteNo;
        public int PartNo;
        public string Lyric = "a";
        public bool ApplyingPreset { get; set; }
        public List<UPhoneme> Phonemes = new List<UPhoneme>();
        public Dictionary<string, UExpression> Expressions = new Dictionary<string, UExpression>();
        public Dictionary<string, UExpression.UExpDiff> VirtualExpressions = new Dictionary<string, UExpression.UExpDiff>();
        public PitchBendExpression PitchBend;
        public VibratoExpression Vibrato;
        public EnvelopeExpression Envelope;


        public bool Error = false;
        public bool Selected = false;

        public int EndTick => PosTick + DurTick;

        public bool IsLyricBoxActive { get; internal set; }

        protected UNote()
        {
            PitchBend = new PitchBendExpression(this);
            Vibrato = new VibratoExpression(this);
            Envelope = new EnvelopeExpression(this);
            Phonemes.Add(new UPhoneme() { Parent = this, PosTick = 0 });
        }

        public static UNote Create() { return new UNote(); }

        public virtual UNote Clone()
        {
            UNote _note = new UNote()
            {
                PosTick = PosTick,
                DurTick = DurTick,
                NoteNum = NoteNum,
                Lyric = Lyric,
                PartNo = PartNo
            };
            _note.Phonemes.Clear();
            foreach (var phoneme in this.Phonemes) _note.Phonemes.Add(phoneme.Clone(_note));
            foreach (var pair in this.Expressions) _note.Expressions.Add(pair.Key, pair.Value.Clone(_note));
            foreach (var pair in this.VirtualExpressions) _note.VirtualExpressions.Add(pair.Key, pair.Value);
            _note.PitchBend = (PitchBendExpression)this.PitchBend.Clone(_note);
            _note.Vibrato = (VibratoExpression)Vibrato.Clone(_note);
            _note.Envelope = (EnvelopeExpression)Envelope.Clone(_note);
            return _note;
        }

        public string GetResamplerFlags()
        {
            StringBuilder flags = new StringBuilder();
            foreach (var item in Expressions)
            {
                if (item.Value is FlagIntExpression fi && fi.Default != (int)fi.Data) {
                    flags.Append(fi.Flag).Append(((int)fi.Data).ToString("+###;-###"));
                }
                else if (item.Value is FlagFloatExpression ff && ff.Default != (float)ff.Data)
                {
                    flags.Append(ff.Flag).Append(((float)ff.Data).ToString("+###.#####;-###.#####"));
                }
                else if (item.Value is FlagBoolExpression fb && fb.Default != (bool)fb.Data)
                {
                    flags.Append(fb.Flag);
                }
                else
                {
                    switch (item.Key)
                    {
                        case "gender":
                            if (((int)item.Value.Data) != 0)
                                flags.Append("g").Append(((int)item.Value.Data).ToString("+###;-###"));
                            break;
                        case "breathiness":
                            if (((int)item.Value.Data) != 100)
                                flags.Append("Y").Append((int)item.Value.Data);
                            break;
                        case "lowpass":
                            if (((int)item.Value.Data) != 0)
                                flags.Append("H").Append((int)item.Value.Data);
                            break;
                        case "highpass":
                        default:
                            break;
                    }
                }
            }
            var flag = flags.ToString();
            //if (!flag.Contains('F')) flag = string.Concat(flag, "F0");
            return flag;
        }

        public int CompareTo(object obj)
        {
            if (obj == null) return 1;

            UNote other = obj as UNote;
            if (other == null)
                throw new ArgumentException("CompareTo object is not a Note");

            if (other.PosTick < this.PosTick)
                return 1;
            else if (other.PosTick > this.PosTick)
                return -1;
            else if (other.GetHashCode() < this.GetHashCode())
                return 1;
            else if (other.GetHashCode() > this.GetHashCode())
                return -1;
            else
                return 0;
        }

        public override string ToString()
        {
            return string.Format("\"{0}\" Pos:{1} Dur:{2} Note:{3}", Lyric, PosTick, DurTick, NoteNum)
                + (Error ? " Error" : "") + (Selected ? " Selected" : "");
        }
    }

    public class UNestableNote : UNote {

        private List<UNote> Notes = new List<UNote>();

        public new int DurTick => Notes.Max(note => note.EndTick) - PosTick;

        public new int PosTick => Notes.Min(note => note.PosTick);

        public int RootNoteNo = 0;

        private UNote RootNote => Notes.Any() && RootNoteNo >= 0 ? Notes[RootNoteNo] : null;

        public new int NoteNum => RootNote?.NoteNum ?? 60;

        public new int PartNo => RootNote?.PartNo ?? -1;
        
        public new static UNestableNote Create() {
            return new UNestableNote();
        }

        public override UNote Clone()
        {
            var note = new UNestableNote() {
                Notes = Notes.Select(ln=>ln.Clone()).ToList()
            };
            return note;
        }
    }

    public class UDictionaryNote
    {
        public SortedList<int, UNote> Notes { get; private set; }
        public Dictionary<int, ExpressionProcessing> NotesProcessing { get; private set; }
        public UDictionaryNote()
        {
            Notes = new SortedList<int, UNote>();
            NotesProcessing = new Dictionary<int, ExpressionProcessing>();
        }

        public UDictionaryNote Clone()
        {
            var a = new UDictionaryNote();
            foreach (var note in Notes)
            {
                a.Notes.Add(note.Key, note.Value.Clone());
            }
            foreach (var item in NotesProcessing)
            {
                a.NotesProcessing.Add(item.Key, item.Value);
            }
            return a;
        }

        public static void ApplyPreset(UNote applyee, UDictionaryNote preset)
        {
            int totallen = preset.Notes.Sum(pair => pair.Value.DurTick);
            int pos = 0;
            applyee.Phonemes.Clear();
            foreach (var note in preset.Notes.Values)
            {
                foreach (var pho in note.Phonemes)
                {
                    var cpho = pho.Clone(applyee);
                    cpho.PosTick += pos;
                    cpho.DurTick = (int)Math.Round((float)cpho.DurTick / totallen * applyee.DurTick);
                    applyee.Phonemes.Add(cpho);
                    pos += cpho.DurTick;
                }
            }
            applyee.ApplyingPreset = true;
        }

        public bool MatchNotes(List<UNote> notes)
        {
            return Notes.Values.SequenceEqual(notes, new LyricsComparer());
        }

        public enum ExpressionProcessing
        {
            Overwrite, Difference, Multiplying
        }

        private class LyricsComparer : IEqualityComparer<UNote>
        {
            public bool Equals(UNote x, UNote y)
            {
                return x.Lyric.Equals(y.Lyric);
            }

            public int GetHashCode(UNote obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
