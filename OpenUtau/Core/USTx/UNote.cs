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
        public string Lyric = "a";
        public List<UPhoneme> Phonemes = new List<UPhoneme>();
        public Dictionary<string, UExpression> Expressions = new Dictionary<string, UExpression>();
        public Dictionary<string, int> VirtualExpressions = new Dictionary<string, int>();
        public PitchBendExpression PitchBend;
        public VibratoExpression Vibrato;
        public bool Error = false;
        public bool Selected = false;

        public int EndTick { get { return PosTick + DurTick; } }

        public bool IsLyricBoxActive { get; internal set; }

        private UNote() {
            PitchBend = new PitchBendExpression(this);
            Vibrato = new VibratoExpression(this);
            Phonemes.Add(new UPhoneme() { Parent = this, PosTick = 0 });
        }

        public static UNote Create() { return new UNote(); }

        public UNote Clone() {
            UNote _note = new UNote()
            {
                PosTick = PosTick,
                DurTick = DurTick,
                NoteNum = NoteNum,
                Lyric = Lyric
            };
            foreach (var phoneme in this.Phonemes) _note.Phonemes.Add(phoneme.Clone(_note));
            foreach (var pair in this.Expressions) _note.Expressions.Add(pair.Key, pair.Value.Clone(_note));
            foreach (var pair in this.VirtualExpressions) _note.VirtualExpressions.Add(pair.Key, pair.Value);
            _note.PitchBend = (PitchBendExpression)this.PitchBend.Clone(_note);
            return _note;
        }

        public string GetResamplerFlags() {
            StringBuilder flags = new StringBuilder();
            foreach (var item in Expressions)
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
}
