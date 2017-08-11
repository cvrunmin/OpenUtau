using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.USTx
{
    public class UOto
    {
        public string Alias { set; get; }
        public string File { set; get; }
        public double Offset { set; get; }
        public double Consonant { set; get; }
        public double Cutoff { set; get; }
        public double Preutter { set; get; }
        public double Overlap { set; get; }
        public double Duration { get; set; }

        public UOto SetAlias(string alia)
        {
            Alias = alia;
            return this;
        }
        public UOto SetFile(string file)
        {
            File = file;
            return this;
        }
        public UOto SetOffset(double offset)
        {
            Offset = offset;
            return this;
        }
        public UOto SetConsonant(double consonant)
        {
            Consonant = consonant;
            return this;
        }
        public UOto SetCutoff(double cutoff)
        {
            Cutoff = cutoff;
            return this;
        }
        public UOto SetPreutter(double preutter)
        {
            Preutter = preutter;
            return this;
        }
        public UOto SetOverlap(double overlap)
        {
            Overlap = overlap;
            return this;
        }

        public UOto SetDuration(double duration)
        {
            Duration = duration;
            return this;
        }
    }

    public class USinger
    {
        public string Name { get; set; } = "";
        public string DisplayName { get { return Loaded ? Name : Name + "[Unloaded]"; } }
        public string Path = "";
        public string Author;
        public string Website;
        public string Language;
        public string Detail;

        public bool Loaded = false;

        public System.Windows.Media.Imaging.BitmapImage Avatar { get; set; }
        public string AvatarPath;

        public Encoding FileEncoding;
        public Encoding PathEncoding;

        public Dictionary<string, string> PitchMap = new Dictionary<string, string>();
        public Dictionary<string, UOto> AliasMap = new Dictionary<string, UOto>();
        public Dictionary<string, UDictionaryNote> PresetLyricsMap = new Dictionary<string, UDictionaryNote>();
    }
}
