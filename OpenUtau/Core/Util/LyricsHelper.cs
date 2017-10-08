using OpenUtau.Core.USTx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.Util
{
    public static class LyricsHelper {
        public static string GetVowel(string lyrics, bool recurse = false)
        {
            if (!recurse)
                try
                {
                    return HiraganaRomajiHelper.GetVowel(lyrics);
                }
                catch (ArgumentException)
                {
                }

            int idx = lyrics.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' });
            return lyrics.Substring(idx != -1 ? idx : 0);
        }

        public static string GetConsonant(string lyrics)
        {
            int idx = lyrics.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' });
            return idx == 0 ? "" : lyrics.Substring(0, idx != -1 ? idx : lyrics.Length);
        }
    }

    public static class HiraganaRomajiHelper
    {
        public static readonly string[] HiraganaColumnA =
        { "あ", "か", "さ", "た", "な",
            "は", "ま", "ら", "が", "ざ",
            "だ", "ば", "ぱ", "や", "わ",
            "きゃ", "しゃ", "ちゃ", "にゃ", "ひゃ",
            "みゃ", "りゃ", "ぎゃ", "じゃ", "びゃ",
            "ぴゃ", "つぁ", "ふぁ", "くぁ", "ぐぁ",
            "ゔぁ", "うぁ", "すぁ", "ずぁ", "ぬぁ",
            "ぶぁ", "ぷぁ", "むぁ","るぁ"
        };
        public static readonly string[] RomajiColumnA =
        { "a", "ka", "sa", "ta", "na",
            "ha", "ma", "ra", "ga", "za",
            "da", "ba", "pa", "ya", "wa",
            "kya", "sha", "cha", "nya", "hya",
            "mya", "rya", "gya", "ja", "bya",
            "pya", "tsa", "fa", "kwa", "gwa",
            "va", "wha", "swa", "zwa", "nwa",
            "bwa", "pwa", "mwa", "rwa"
        };
        public static readonly string[] HiraganaColumnI =
        { "い", "き", "し", "ち", "に",
            "ひ", "み", "り", "ぎ", "じ",
             "び", "ぴ", "てぃ", "ふぃ","でぃ",
            "くぃ", "ぐぃ", "ゔぃ", "うぃ","つぃ",
            "すぃ", "ずぃ", "ぬぃ", "ぶぃ", "ぷぃ",
            "むぃ", "るぃ"
        };
        public static readonly string[] RomajiColumnI =
        { "i", "ki", "shi", "chi", "ni",
            "hi", "mi", "ri", "gi", "ji",
             "bi", "pi", "ti", "fi","di",
            "kwi", "gwi", "vi", "wi","tsi",
            "swi", "zwi", "nwi", "bwi", "pwi",
            "mwi", "rwi"
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
            "gyu", "ju", "byu", "pyu", "dyu",
            "tou", "dou", "tyu", "vu", "fyu",
            "vyu"
        };
        public static readonly string[] HiraganaColumnE =
        { "え", "け", "せ", "て", "ね",
            "へ", "め", "れ", "げ", "ぜ",
            "で", "べ", "ぺ", "しぇ","ちぇ",
            "うぇ", "くぇ", "ぐぇ", "つぇ", "ふぇ", "じぇ",
            "いぇ", "ゔぇ", "きぇ", "ぎぇ",
            "すぇ", "ずぇ", "にぇ", "ぬぇ",
            "ひぇ","びぇ","ぴぇ", "ぶぇ", "ぷぇ",
            "みぇ", "むぇ","りぇ", "るぇ"
        };
        public static readonly string[] RomajiColumnE =
        { "e", "ke", "se", "te", "ne",
            "he", "me", "re", "ge", "ze",
            "de", "be", "pe", "she","che",
            "we", "kwe", "gwe", "tse", "fe", "je",
            "ye", "ve", "kye", "gye",
            "swe", "zwe", "nye", "nwe",
            "hye", "bye", "pye", "bwe", "pwi",
            "mye", "mwe","rye", "rwe"
        };
        public static readonly string[] HiraganaColumnO =
        { "お", "こ", "そ", "と", "の",
            "ほ", "も", "ろ", "ご", "ぞ",
            "ど", "ぼ", "ぽ", "よ", "を",
            "きょ", "しょ", "ちょ", "にょ", "ひょ",
            "みょ", "りょ", "ぎょ", "じょ", "びょ",
            "ぴょ", "つぉ", "ふぉ","うぉ", "くぉ", "ぐぉ",
            "ゔぉ", "すぉ", "ずぉ", "ぬぉ",
            "ぶぉ", "ぷぉ", "むぉ", "るぉ"
        };
        public static readonly string[] RomajiColumnO =
        { "o", "ko", "so", "to", "no",
            "ho", "mo", "ro", "go", "zo",
            "do", "bo", "po", "yo", "wo",
            "kyo", "sho", "cho", "nyo", "hyo",
            "myo", "ryo", "gyo", "jo", "byo",
            "pyo", "tso", "fo","who", "kwo", "gwo",
            "vo", "swo", "zwo", "nwo",
            "bwo", "pwo", "mwo", "rwo"
        };

        public static string GetVowel(string lyrics)
        {
            if (HiraganaColumnA.Contains(lyrics))
            {
                return "a";
            }
            if (HiraganaColumnI.Contains(lyrics))
            {
                return "i";
            }
            if (HiraganaColumnU.Contains(lyrics))
            {
                return "u";
            }
            if (HiraganaColumnE.Contains(lyrics))
            {
                return "e";
            }
            if (HiraganaColumnO.Contains(lyrics))
            {
                return "o";
            }
            if (lyrics.Equals("ん")) return "n";
            if (lyrics.All(ch => char.IsLetter(ch))) return LyricsHelper.GetVowel(lyrics, true);
            throw new ArgumentException($"Unsupported parameter: {lyrics}", nameof(lyrics));
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
                case "n":
                    return "n";
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
                case 'n':
                    return "ん";
                default:
                    throw new ArgumentException($"Unsupported parameter: {romaji}", nameof(romaji));
            }
        }

        public static bool IsSupportedHiragana(string proofing)
        {
            return "ん".Equals(proofing) || HiraganaColumnA.Contains(proofing) || HiraganaColumnE.Contains(proofing) || HiraganaColumnI.Contains(proofing) || HiraganaColumnO.Contains(proofing) || HiraganaColumnU.Contains(proofing);
        }
        public static bool IsSupportedRomaji(string proofing)
        {
            return "n".Equals(proofing) || RomajiColumnA.Contains(proofing) || RomajiColumnE.Contains(proofing) || RomajiColumnI.Contains(proofing) || RomajiColumnO.Contains(proofing) || RomajiColumnU.Contains(proofing);
        }
    }

    public class SamplingStyleHelper {
        public enum Style
        {
            CV = 1,
            VC = 2,
            VCV = 3,
            CVVC = 5,
            Others = 0
        }

        public static string GetCorrespondingPhoneme(string original, UNote former, UNote lator, Style dest) {
            Style style = GetStyle(original);
            if (style == Style.CV)
            {
                if (dest == Style.VCV)
                {
                    if (former != null)
                    {
                        if (HiraganaRomajiHelper.IsSupportedHiragana(former.Lyric))
                        {
                            return HiraganaRomajiHelper.GetVowel(former.Lyric) + " " + original;
                        }
                        else if (HiraganaRomajiHelper.IsSupportedRomaji(former.Lyric))
                        {
                            return former.Lyric.Last() + " " + original;
                        }
                        else
                        {
                            var wildGuessVowel = former.Lyric.Substring(former.Lyric.IndexOfAny(new char[]{ 'a', 'e', 'i', 'o', 'u'}));
                            return wildGuessVowel + " " + original;
                        }
                    }
                    else
                    {
                        return "- " + original;
                    }
                }
                else if(dest == Style.CVVC)
                {
                    //TODO Smart CV2CVVC conversion
                    if (former == null || GetStyle(former.Lyric) == Style.VCV)
                    {

                    }
                }
            }
            else if (style == Style.VCV)
            {
                if (dest == Style.CV)
                {
                    return original.Substring(original.IndexOf(' ') + 1);
                }
            }
            else if (style == Style.CVVC)
            {
                //TODO smart CVVC2CV conversion
            }
            return original;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="phoneme"></param>
        /// <returns></returns>
        public static Style GetStyle(string phoneme) {
            var match = System.Text.RegularExpressions.Regex.Match(phoneme, pattern: @"(\w+)\s(\w+)");
            if (match.Success)
            {
                if (match.Groups[1].Value == "-")
                {
                    return Style.VCV;
                }
                else
                {
                    return Style.CVVC;
                }
            }
            else if(System.Text.RegularExpressions.Regex.IsMatch(phoneme, pattern: @"\w+"))
            {
                return Style.CV;
            }
            else
            {
                return Style.Others;
            }
        }
    }
}
