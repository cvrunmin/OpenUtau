using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.USTx
{
    public class UOto : IComparable<UOto>
    {
        public string Alias { set; get; }
        public string File { set; get; }
        public double Offset { set; get; }
        public double Consonant { set; get; }
        public double Cutoff { set; get; }
        public double Preutter { set; get; }
        public double Overlap { set; get; }
        public double Duration { get; set; }

        public UOto() { }

        public UOto(UOto clone) {
            Alias = clone.Alias;
            File = clone.File;
            Offset = clone.Offset;
            Consonant = clone.Consonant;
            Cutoff = clone.Cutoff;
            Preutter = clone.Preutter;
            Overlap = clone.Overlap;
            Duration = clone.Duration;
        }

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

        public int CompareTo(UOto other)
        {
            return Alias.CompareTo(other.Alias);
        }
    }

    public class USinger
    {
        public string Name { get; set; } = "";
        public string DisplayName => Loaded ? Name : Name + "[Unloaded]";
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

        public Util.SamplingStyleHelper.Style Style;

        public Dictionary<string, string> PitchMap = new Dictionary<string, string>();
        public SortedDictionary<string, UOto> AliasMap = new SortedDictionary<string, UOto>();
        public SortedDictionary<string, SortedSet<string>> ConsonentMap = new SortedDictionary<string, SortedSet<string>>();
        public SortedDictionary<string, SortedSet<string>> ConsonentRawMap = new SortedDictionary<string, SortedSet<string>>();
        public SortedDictionary<string, SortedSet<string>> VowelMap = new SortedDictionary<string, SortedSet<string>>();
        public SortedDictionary<string, SortedSet<string>> VowelRawMap = new SortedDictionary<string, SortedSet<string>>();
        public SortedDictionary<string, UDictionaryNote> PresetLyricsMap = new SortedDictionary<string, UDictionaryNote>();
    }
}
