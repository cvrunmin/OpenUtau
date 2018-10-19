using System;
using System.Linq;
using System.Text.RegularExpressions;
using OpenUtau.Core.USTx;

namespace OpenUtau.Core.Util
{
    public static class LyricsHelper {
        public static string GetVowel(string lyrics, bool recurse = false)
        {
            if (!recurse && HiraganaRomajiHelper.IsSupported(lyrics))
                try
                {
                    return HiraganaRomajiHelper.GetVowel(lyrics);
                }
                catch (ArgumentException)
                {
                }
            switch (SamplingStyleHelper.GetStyle(lyrics)) {
                default:
                case SamplingStyleHelper.Style.CV:
                    {
                        int idx = lyrics.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' });
                        return lyrics.Substring(idx != -1 ? idx : 0);
                    }
                case SamplingStyleHelper.Style.VCV:
                    {
                        lyrics = lyrics.Split(' ')[1];
                        int idx = lyrics.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' });
                        return lyrics.Substring(idx != -1 ? idx : 0);
                    }
                case SamplingStyleHelper.Style.VC:
                    {
                        return lyrics.Split(' ')[0];
                    }
            }
        }

        public static string GetVowel(string lyrics, USinger singer)
        {
            var vowel = singer?.VowelMap?.DeRedirect().FirstOrDefault(pair => pair.Value.Contains(lyrics)).Key;
            return string.IsNullOrEmpty(vowel) ? GetVowel(lyrics) : vowel;
        }

        public static string GetConsonant(string lyrics)
        {
            if (HiraganaRomajiHelper.IsSupportedHiragana(lyrics))
                try
                {
                    return HiraganaRomajiHelper.ToRomaji(lyrics).Replace(HiraganaRomajiHelper.GetVowel(lyrics), "");
                }
                catch (ArgumentException)
                {
                }
            switch (SamplingStyleHelper.GetStyle(lyrics))
            {
                default:
                case SamplingStyleHelper.Style.CV:
                    {
                        int idx = lyrics.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' });
                        return idx == 0 ? "" : lyrics.Substring(0, idx != -1 ? idx : lyrics.Length);
                    }
                case SamplingStyleHelper.Style.VCV:
                    {
                        lyrics = lyrics.Split(' ')[1];
                        int idx = lyrics.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' });
                        return idx == 0 ? "" : lyrics.Substring(0, idx != -1 ? idx : lyrics.Length);
                    }
                case SamplingStyleHelper.Style.VC:
                    {
                        return lyrics.Split(' ')[1];
                    }
            }
        }

        public static string GetConsonant(string lyrics, USinger singer)
        {
            var consonant = singer?.ConsonentMap?.DeRedirect().FirstOrDefault(pair => pair.Value.Contains(lyrics)).Key;
            return string.IsNullOrEmpty(consonant) ? GetConsonant(lyrics) : consonant;
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
            "ぶぁ", "ぷぁ", "むぁ","るぁ", "てゃ", "でゃ"
        };
        public static readonly string[] RomajiColumnA =
        { "a", "ka", "sa", "ta", "na",
            "ha", "ma", "ra", "ga", "za",
            "da", "ba", "pa", "ya", "wa",
            "kya", "sha", "cha", "nya", "hya",
            "mya", "rya", "gya", "ja", "bya",
            "pya", "tsa", "fa", "kwa", "gwa",
            "va", "wha", "swa", "zwa", "nwa",
            "bwa", "pwa", "mwa", "rwa", "tya", "dha"
        };
        public static readonly string[] HiraganaColumnI =
        { "い", "き", "し", "ち", "に",
            "ひ", "み", "り", "ぎ", "じ",
             "び", "ぴ", "てぃ", "ふぃ", "てぃ","でぃ",
            "くぃ", "ぐぃ", "ゔぃ", "うぃ","つぃ",
            "すぃ", "ずぃ", "ぬぃ", "ぶぃ", "ぷぃ",
            "むぃ", "るぃ"
        };
        public static readonly string[] RomajiColumnI =
        { "i", "ki", "shi", "chi", "ni",
            "hi", "mi", "ri", "gi", "ji",
             "bi", "pi", "ti", "fi", "tyi", "dhi",
            "kwi", "gwi", "vi", "wi","tsi",
            "swi", "zwi", "nwi", "bwi", "pwi",
            "mwi", "rwi"
        };
        public static readonly string[] HiraganaColumnU =
        { "う", "く", "す", "つ", "ぬ",
            "ふ", "む", "る", "ぐ", "ず",
            "ぶ", "ぷ", "ゆ", "きゅ", "しゅ",
            "ちゅ", "にゅ", "ひゅ", "みゅ", "りゅ",
            "ぎゅ", "じゅ", "びゅ", "ぴゅ", "てゅ", "でゅ",
            "とぅ", "どぅ", "てゅ", "ゔ", "ふゅ",
            "ゔゅ"
        };
        public static readonly string[] RomajiColumnU =
        { "u", "ku", "su", "tsu", "nu",
            "fu", "mu", "ru", "gu", "zu",
            "bu", "pu", "yu", "kyu", "shu",
            "chu", "nyu", "hyu", "myu", "ryu",
            "gyu", "ju", "byu", "pyu", "tyu", "dhu",
            "tou", "du", "tyu", "vu", "fyu",
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
            "みぇ", "むぇ","りぇ", "るぇ", "てぇ", "でぇ"
        };
        public static readonly string[] RomajiColumnE =
        { "e", "ke", "se", "te", "ne",
            "he", "me", "re", "ge", "ze",
            "de", "be", "pe", "she","che",
            "we", "kwe", "gwe", "tse", "fe", "je",
            "ye", "ve", "kye", "gye",
            "swe", "zwe", "nye", "nwe",
            "hye", "bye", "pye", "bwe", "pwi",
            "mye", "mwe","rye", "rwe", "tye", "dhe"
        };
        public static readonly string[] HiraganaColumnO =
        { "お", "こ", "そ", "と", "の",
            "ほ", "も", "ろ", "ご", "ぞ",
            "ど", "ぼ", "ぽ", "よ", "を",
            "きょ", "しょ", "ちょ", "にょ", "ひょ",
            "みょ", "りょ", "ぎょ", "じょ", "びょ",
            "ぴょ", "つぉ", "ふぉ","うぉ", "くぉ", "ぐぉ",
            "ゔぉ", "すぉ", "ずぉ", "ぬぉ",
            "ぶぉ", "ぷぉ", "むぉ", "るぉ", "てゃ", "でゃ"
        };
        public static readonly string[] RomajiColumnO =
        { "o", "ko", "so", "to", "no",
            "ho", "mo", "ro", "go", "zo",
            "do", "bo", "po", "yo", "wo",
            "kyo", "sho", "cho", "nyo", "hyo",
            "myo", "ryo", "gyo", "jo", "byo",
            "pyo", "tso", "fo","who", "kwo", "gwo",
            "vo", "swo", "zwo", "nwo",
            "bwo", "pwo", "mwo", "rwo", "tyo", "dho"
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
            if (lyrics.All(char.IsLetter)) return LyricsHelper.GetVowel(lyrics, true);
            throw new ArgumentException($@"Unsupported parameter: {lyrics}", nameof(lyrics));
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
                    throw new ArgumentException($@"Unsupported parameter: {hiragana}", nameof(hiragana));
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
                    throw new ArgumentException($@"Unsupported parameter: {romaji}", nameof(romaji));
            }
        }

        public static bool IsSupported(string proofing) {
            return IsSupportedHiragana(proofing) || IsSupportedRomaji(proofing);
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

        public static string GetCorrespondingPhoneme(string original, UNote former, UNote lator, Style dest, USinger singer = null) {
            Style style = GetStyle(original);
            if (original.Equals("-")) return original;
            if (style == Style.CV)
            {
                if (dest == Style.VCV)
                {
                    string GetDestPho(bool romaji) {
                        var neworiginal = romaji && HiraganaRomajiHelper.IsSupportedHiragana(original) ? HiraganaRomajiHelper.ToRomaji(original) : original;
                        if (former != null)
                        {
                            string mainPho;
                            if (former.Lyric.Equals("-"))
                            {
                                mainPho = former.Phonemes[0].Phoneme;
                            }
                            else
                            {
                                var lyrics = former.Lyric.Split(' ');
                                if (lyrics.Length == 1)
                                {
                                    mainPho = lyrics[0];
                                }
                                else
                                {
                                    mainPho = lyrics[1];
                                }
                            }
                            if (HiraganaRomajiHelper.IsSupportedHiragana(mainPho))
                            {
                                return HiraganaRomajiHelper.GetVowel(mainPho) + " " + neworiginal;
                            }
                            if (HiraganaRomajiHelper.IsSupportedRomaji(mainPho))
                            {
                                return mainPho.Last() + " " + neworiginal;
                            }
                            int startIndex = mainPho.IndexOfAny(new[] { 'a', 'e', 'i', 'o', 'u' });
                            var wildGuessVowel = mainPho.Substring(Math.Max(0, startIndex));
                            return wildGuessVowel + " " + neworiginal;
                        }
                        return "- " + neworiginal;
                    }
                    string trial = GetDestPho(false);
                    return singer?.AliasMap.ContainsKey(trial) != false ? trial : GetDestPho(true);
                }
                if(dest == Style.CVVC)
                {
                    string pt1 = "";
                    string pt2 = "";
                    //TODO Smart CV2CVVC conversion
                    if (former == null) {
                        pt1 = "- " + original;
                    }
                    else
                    {
                        pt1 = original;
                    }
                    if (lator == null) {
                        pt2 = LyricsHelper.GetVowel(original, singer) + " R";
                    }
                    else
                    {
                        Style style1 = GetStyle(lator.Lyric);
                        if (style1 == Style.VC || style1 == Style.VCV) return pt1;
                        string con = LyricsHelper.GetConsonant(lator.Lyric, singer);
                        pt2 = LyricsHelper.GetVowel(original, singer) + " " + (string.IsNullOrEmpty(con) ? LyricsHelper.GetVowel(lator.Lyric, singer) : con);
                    }
                    return pt1 + '\t' + pt2;
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
            var match = Regex.Match(phoneme, pattern: @"([\w-]+)\s(\w+)");
            if (match.Success)
            {
                if (match.Groups[1].Value == "-")
                {
                    return Style.VCV;
                }
                if (HiraganaRomajiHelper.IsSupportedHiragana(match.Groups[2].Value) || HiraganaRomajiHelper.IsSupportedRomaji(match.Groups[2].Value))
                    return Style.VCV;
                return Style.VC;
            }
            if(Regex.IsMatch(phoneme, pattern: @"[\w-]+"))
            {
                return Style.CV;
            }
            return Style.Others;
        }
    }
}
