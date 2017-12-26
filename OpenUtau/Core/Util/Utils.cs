using OpenUtau.Core.USTx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Controls;

namespace OpenUtau.Core.Util
{
    static class Utils
    {
        public static void SetExpresstionValue(UExpression expression, object obj)
        {
            if (expression is IntExpression)
            {
                expression.Data = (int)obj;
            }
            if (expression is FloatExpression)
            {
                expression.Data = (float)obj;
            }
        }
        public static void SetExpresstionValue(UExpression expression, string obj)
        {
            if (expression is IntExpression)
            {
                expression.Data = int.Parse(obj);
            }
            if (expression is FloatExpression)
            {
                expression.Data = float.Parse(obj);
            }
        }

        public static List<UPart> SplitPart(UPart src, int splitTick) {
            var dest1 = src.UClone();
            dest1.DurTick = splitTick - dest1.PosTick;
            if (dest1 is UWavePart wave)
            {
                wave.HeadTrimTick = ((UWavePart)src).HeadTrimTick;
                wave.TailTrimTick = wave.FileDurTick - wave.HeadTrimTick - wave.DurTick;
            } else if (dest1 is UVoicePart voice) {
                voice.Notes.RemoveWhere(note => note.PosTick >= voice.DurTick);
            }
            var dest2 = src.UClone();
            dest2.DurTick = dest2.EndTick - splitTick;
            dest2.PosTick = splitTick;
            if (dest2 is UWavePart wave1)
            {
                wave1.HeadTrimTick = ((UWavePart)dest1).HeadTrimTick + dest1.DurTick;
                wave1.TailTrimTick = ((UWavePart)src).TailTrimTick;
            } else if (dest2 is UVoicePart voice1) {
                voice1.Notes.RemoveWhere(note => note.PosTick < dest1.DurTick);
                foreach (var item in voice1.Notes)
                {
                    item.PosTick -= dest1.DurTick;
                }
            }
            return new List<UPart>() { dest1, dest2 };
        }
        public static UPart MergeParts(int preferedPartNo, params UPart[] parts) {
            var wp = parts.OfType<UWavePart>();
            var vp = parts.OfType<UVoicePart>();
            if (wp.Count() > 0 && vp.Count() > 0) throw new ArgumentException("pure uparts arrays should be provided", nameof(parts));
            if (wp.Count() == 0)
            {
                return MergeParts(preferedPartNo, vp.ToArray());
            }
            if (vp.Count() == 0)
            {
                return MergeParts(preferedPartNo, wp.ToArray());
            }
            return null;
        }
        public static UVoicePart MergeParts(int preferedPartNo, params UVoicePart[] parts) {
            if (!ValidateMergingParts(parts))
            {
                throw new ArgumentException("Not all parts are in the same track", nameof(parts));
            }
            parts = parts.OrderBy(part => part.PosTick).ToArray();
            UVoicePart dest = parts[0].UClone() as UVoicePart;
            dest.PartNo = preferedPartNo;
            dest.TrackNo = parts[0].TrackNo;
            int noteNo = 0;
            var relativePosTick = dest.DurTick;
            for (int i = 1; i < parts.Length; i++)
            {
                int posCorr = (parts[i].PosTick - parts[i - 1].EndTick);
                dest.DurTick += parts[i].DurTick + posCorr;
                foreach (var note in parts[i].Notes)
                {
                    var cloned = note.Clone();
                    cloned.NoteNo = noteNo;
                    cloned.PartNo = preferedPartNo;
                    cloned.PosTick += relativePosTick + posCorr;
                    foreach (var item in cloned.Expressions)
                    {
                        if (item.Value is IntExpression)
                        {
                            cloned.VirtualExpressions[item.Key].Data = ((int)dest.Expressions[item.Key].Data) - (int)cloned.Expressions[item.Key].Data;
                        }
                        if (item.Value is FloatExpression)
                        {
                            cloned.VirtualExpressions[item.Key].Data = ((float)dest.Expressions[item.Key].Data) - (float)cloned.Expressions[item.Key].Data;
                        }
                        if (item.Value is BoolExpression)
                        {
                            cloned.VirtualExpressions[item.Key].Data = ((bool)dest.Expressions[item.Key].Data) != (bool)cloned.Expressions[item.Key].Data;
                        }
                    }
                    dest.Notes.Add(cloned);
                    ++noteNo;
                }
                relativePosTick = dest.DurTick;
            }
            return dest;
        }

        private static bool ValidateMergingParts(UVoicePart[] parts) {
            var expectedTrackNo = parts[0].TrackNo;
            foreach (var part in parts)
            {
                if (part.TrackNo != expectedTrackNo)
                {
                    return false;
                }
            }
            return true;
        }

        public static UWavePart MergeParts(int expectedPartNo, params UWavePart[] parts) {
            ValidateMergingParts(parts);
            UWavePart dest = new UWavePart() { PartNo = expectedPartNo, FilePath = parts[0].FilePath, PosTick = parts[0].PosTick, DurTick = parts[0].DurTick, TrackNo = parts[0].TrackNo, FileDurMillisecond = parts[0].FileDurMillisecond, FileDurTick = parts[0].FileDurTick, Channels = parts[0].Channels};
            dest.HeadTrimTick = parts[0].HeadTrimTick;
            dest.TailTrimTick = parts[parts.Length - 1].TailTrimTick;
            dest.DurTick = parts.Sum(part => part.DurTick);
            return dest;
        }

        private static void ValidateMergingParts(UWavePart[] parts)
        {
            var expectedTrackNo = parts[0].TrackNo;
            var expectedFile = parts[0].FilePath;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].TrackNo != expectedTrackNo)
                {
                    throw new ArgumentException("Not all parts are in the same track", nameof(parts));
                }
                if (parts[i].FilePath != expectedFile)
                {
                    throw new ArgumentException("Not all parts have the same file", nameof(parts));
                }
                if (i > 0 && parts[i].EndTick != parts[i - 1].PosTick)
                {
                    throw new ArgumentException("Non-continous parts", nameof(parts));
                }
            }
        }

        public static (bool flag, string match) StartsWithAny(this string str, IEnumerable<string> values) {
            foreach (var value in values)
            {
                if (str.StartsWith(value)) return (true, value);
            }
            return (false, "");
        }

        public static bool ContainsAny(this string str, IEnumerable<string> values, out string match) {
            foreach (var val in values)
            {
                if (str.Contains(val))
                {
                    match = val;
                    return true;
                }
            }
            match = "";
            return false;
        }

        public static string MakeCompletePhonemes(params string[] phos) {
            string r = phos[0];
            for (int i = 1; i < phos.Length; i++)
            {
                r = r.Remove(r.IndexOf(FindOverlapping(r, phos[i]))) + phos[i];
            }
            return r;
        }

        public static string FindOverlapping(string a, string b)
        {
            for (int i = a.Length - 1; i >= 0; i--)
            {
                if (a[i] == b[0])
                {
                    bool flag = true;
                    for (int j = 0; j < a.Length - i; j++)
                    {
                        if (a[i + j] != b[j])
                        {
                            flag = false;
                            break;
                        }
                    }
                    if (flag) return a.Substring(i);
                }
            }
            return "";
        }
    }

    public static class ColorsConverter
    {
        public static Color ToMediaColor(this System.Drawing.Color drawingColor) => 
            new Color() { R = drawingColor.R, G = drawingColor.G, B = drawingColor.B, A = drawingColor.A };

        public static System.Drawing.Color ToDrawingColor(this Color mediaColor) =>
            System.Drawing.Color.FromArgb(red: mediaColor.R, green: mediaColor.G, blue: mediaColor.B, alpha: mediaColor.A);
    }

    public class NumericValidationRule : ValidationRule
    {
        public Type ValidationType { get; set; }
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string strValue = Convert.ToString(value);

            if (string.IsNullOrEmpty(strValue))
                return new ValidationResult(false, $"Value cannot be coverted to string.");
            bool canConvert = false;
            switch (ValidationType.Name)
            {

                case "Boolean":
                    bool boolVal = false;
                    canConvert = bool.TryParse(strValue, out boolVal);
                    return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of boolean");
                case "Int32":
                    int intVal = 0;
                    canConvert = int.TryParse(strValue, out intVal);
                    return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Int32");
                case "Double":
                    double doubleVal = 0;
                    canConvert = double.TryParse(strValue, out doubleVal);
                    return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Double");
                case "Int64":
                    long longVal = 0;
                    canConvert = long.TryParse(strValue, out longVal);
                    return canConvert ? new ValidationResult(true, null) : new ValidationResult(false, $"Input should be type of Int64");
                default:
                    throw new InvalidCastException($"{ValidationType.Name} is not supported");
            }
        }
    }

    public class CVMapCompare : IEqualityComparer<KeyValuePair<string, SortedSet<string>>>
    {
        public bool Equals(KeyValuePair<string, SortedSet<string>> x, KeyValuePair<string, SortedSet<string>> y)
        {
            return x.Key.Equals(y.Key);
        }

        public int GetHashCode(KeyValuePair<string, SortedSet<string>> obj)
        {
            return obj.Key.GetHashCode();
        }
    }
}
