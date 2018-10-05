using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OpenUtau.Core.Lib;

namespace OpenUtau.Core.Formats
{
    public class Presamp
    {
        public enum Locale {
            Japanese, English, Korean, Chinese
        }

        private const string VersionTag = "[VERSION]";
        private const string LocaleTag = "[LOCALE]";
        private const string ResamplerTag = "[RESAMP]";
        private const string WavToolTag = "[TOOL]";
        private const string VowelTag = "[VOWEL]";
        private const string ConsonantTag = "[CONSONANT]";
        private const string Priorityag = "[PRIORITY]";
        private const string ReplaceTag = "[REPLACE]";
        private const string AliasTag = "[ALIAS]";
        private const string PrefixTag = "[PRE]";
        private const string EndFlagTag = "[ENDFLAG]";
        private const string EndTypeTag = "[ENDTYPE]";
        private const string VCLengthTag = "[VCLENGTH]";
        private const string MustVCTag = "[MUSTVC]";
        private const string CFlagsTag = "[CFLAGS]";
        private const string SuTag = "[SU]";


        public SortedDictionary<string, VCContent> ConsonentMap = new SortedDictionary<string, VCContent>();
        public SortedDictionary<string, VCContent> VowelMap = new SortedDictionary<string, VCContent>();

        public Presamp() {
            
        }

        public static Presamp Load(string file) {
            if (File.Exists(file))
            {
                var presamp = new Presamp();
                using (var reader = new StreamReader(File.OpenRead(file), EncodingUtil.DetectFileEncoding(file))) {
                    var currentBlock = "";
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine().Trim();
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            currentBlock = line;
                        }
                        else {
                            switch (currentBlock) {
                                case VowelTag:
                                    var vparts = line.Split('=');
                                    if (vparts.Length != 4)
                                        continue;
                                    if (vparts[0] != vparts[1]) {
                                        presamp.VowelMap.Add(vparts[1], new VCContent() { IsRedirect = true, RedirectTo = vparts[0] });
                                    }
                                    var lyrics = vparts[2].Split(',');
                                    var sortset = new SortedSet<string>(lyrics);
                                    presamp.VowelMap.Add(vparts[0], new VCContent() { IsRedirect = false, Content = sortset });
                                    break;
                                case ConsonantTag:
                                    var cparts = line.Split('=');
                                    if (cparts.Length < 3 || cparts.Length > 4)
                                        continue;
                                    var csset = new SortedSet<string>(cparts[1].Split(','));
                                    if (presamp.ConsonentMap.ContainsKey(cparts[0])) {
                                        presamp.ConsonentMap[cparts[0]].Content = new SortedSet<string>(presamp.ConsonentMap[cparts[0]].Content.Concat(csset));
                                    }else
                                    {
                                        presamp.ConsonentMap.Add(cparts[0], new VCContent() { IsRedirect = false, Content = csset });
                                    }

                                    break;
                            }
                        }
                    }
                }
                return presamp;
            }
            return null;
        }

        public class VCContent {
            public bool IsRedirect { get; set; }
            public string RedirectTo { get; set; }
            public SortedSet<string> Content { get; set; }
        }

    }
   
}
