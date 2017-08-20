using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.Util
{
    class HiraganaRomajiHelper
    {
        public static readonly string[] HiraganaColumnA =
        { "あ", "か", "さ", "た", "な",
            "は", "ま", "ら", "が", "ざ",
            "だ", "ば", "ぱ", "や", "わ",
            "きゃ", "しゃ", "ちゃ", "にゃ", "ひゃ",
            "みゃ", "りゃ", "ぎゃ", "じゃ", "びゃ",
            "ぴゃ", "つぁ", "ふぁ", "くぁ", "ぐぁ",
            "ゔぁ"
        };
        public static readonly string[] RomajiColumnA =
        { "a", "ka", "sa", "ta", "na",
            "ha", "ma", "ra", "ga", "za",
            "da", "ba", "pa", "ya", "wa",
            "kya", "sha", "cha", "nya", "hya",
            "mya", "rya", "gya", "jya", "bya",
            "pya", "tsa", "fa", "kwa", "gwa",
            "va"
        };
        public static readonly string[] HiraganaColumnI =
        { "い", "き", "し", "ち", "に",
            "ひ", "み", "り", "ぎ", "じ",
             "び", "ぴ", "てぃ", "ふぃ","でぃ",
            "くぃ", "ゔぃ", "うぃ","つぃ"
        };
        public static readonly string[] RomajiColumnI =
        { "i", "ki", "shi", "chi", "ni",
            "hi", "mi", "ri", "gi", "ji",
             "bi", "pi", "ti", "fi","di",
            "kwi", "vi", "wi","tsi"
        };
        public static readonly string[] HiraganaColumnU =
        { "う", "く", "す", "つ", "ぬ",
            "ふ", "む", "る", "ぐ", "ず",
            "ぶ", "ぷ", "ゆ", "きゅ", "しゅ",
            "ちゅ", "にゅ", "ひゅ", "みゅ", "りゅ",
            "ぎゅ", "じゅ", "びゅ", "ぴゅ", "でゅ",
            "とぅ", "どぅ", "てゅ", "ゔ", "ふゅ",
            "ゔゅ"
        };
        public static readonly string[] RomajiColumnU =
        { "u", "ku", "su", "tsu", "nu",
            "fu", "mu", "ru", "gu", "zu",
            "bu", "pu", "yu", "kyu", "shu",
            "chu", "nyu", "hyu", "myu", "ryu",
            "gyu", "jyu", "byu", "pyu", "dyu",
            "tou", "dou", "tyu", "vu", "fyu",
            "vyu"
        };
        public static readonly string[] HiraganaColumnE =
        { "え", "け", "せ", "て", "ね",
            "へ", "め", "れ", "げ", "ぜ",
            "で", "べ", "ぺ", "しぇ","ちぇ",
            "うぇ", "くぇ", "つぇ", "ふぇ", "じぇ",
            "いぇ", "ゔぇ" };
        public static readonly string[] RomajiColumnE =
        { "e", "ke", "se", "te", "ne",
            "he", "me", "re", "ge", "se",
            "de", "be", "pe", "she","che",
            "we", "kwe", "tse", "fe", "je",
            "ye", "ve" };
        public static readonly string[] HiraganaColumnO =
        { "お", "こ", "そ", "と", "の",
            "ほ", "も", "ろ", "ご", "ぞ",
            "ど", "ぼ", "ぽ", "よ", "を",
            "きょ", "しょ", "ちょ", "にょ", "ひょ",
            "みょ", "りょ", "ぎょ", "じょ", "びょ",
            "ぴょ", "つぉ", "ふぉ","うぉ", "くぉ",
            "ゔぉ" };
        public static readonly string[] RomajiColumnO =
        { "o", "ko", "so", "to", "no",
            "ho", "mo", "ro", "go", "zo",
            "do", "bo", "po", "yo", "wo",
            "kyo", "sho", "cho", "nyo", "hyo",
            "myo", "ryo", "gyo", "jyo", "byo",
            "pyo", "tso", "fo","who", "kwo",
            "vo" };

        public static string GetVowel(string hiragana)
        {
            if (HiraganaColumnA.Contains(hiragana))
            {
                return "a";
            }
            if (HiraganaColumnI.Contains(hiragana))
            {
                return "i";
            }
            if (HiraganaColumnU.Contains(hiragana))
            {
                return "u";
            }
            if (HiraganaColumnE.Contains(hiragana))
            {
                return "e";
            }
            if (HiraganaColumnO.Contains(hiragana))
            {
                return "o";
            }
            throw new ArgumentException($"Unsupported parameter: {hiragana}", nameof(hiragana));
        }

        public static string ToRomaji(string hiragana)
        {
            switch (GetVowel(hiragana))
            {
                case "a":
                    return RomajiColumnA[HiraganaColumnA.ToList().IndexOf(hiragana)];
                case "i":
                    return RomajiColumnI[HiraganaColumnI.ToList().IndexOf(hiragana)];
                case "u":
                    return RomajiColumnU[HiraganaColumnU.ToList().IndexOf(hiragana)];
                case "e":
                    return RomajiColumnE[HiraganaColumnE.ToList().IndexOf(hiragana)];
                case "o":
                    return RomajiColumnO[HiraganaColumnO.ToList().IndexOf(hiragana)];
                default:
                    throw new ArgumentException($"Unsupported parameter: {hiragana}", nameof(hiragana));
            }
        }
        public static string ToHiragana(string romaji) {
            switch (romaji.Last())
            {
                case 'a':
                    return HiraganaColumnA[RomajiColumnA.ToList().IndexOf(romaji)];
                case 'i':
                    return HiraganaColumnI[RomajiColumnI.ToList().IndexOf(romaji)];
                case 'u':
                    return HiraganaColumnU[RomajiColumnU.ToList().IndexOf(romaji)];
                case 'e':
                    return HiraganaColumnE[RomajiColumnE.ToList().IndexOf(romaji)];
                case 'o':
                    return HiraganaColumnO[RomajiColumnO.ToList().IndexOf(romaji)];
                default:
                    throw new ArgumentException($"Unsupported parameter: {romaji}", nameof(romaji));
            }
        }
    }
}
