using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using OpenUtau.Core.USTx;
using OpenUtau.Core.Lib;
using NAudio.Wave;
using System.Web.Script.Serialization;
using static OpenUtau.Core.Formats.USTx;

namespace OpenUtau.Core.Formats
{
    public static class UtauSoundbank
    {
        public static Dictionary<string, USinger> FindAllSingers()
        {
            Dictionary<string, USinger> singers = new Dictionary<string, USinger>();
            var singerSearchPaths = PathManager.Inst.GetSingerSearchPaths();
            foreach (string searchPath in singerSearchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;
                foreach (var dirpath in Directory.EnumerateDirectories(searchPath))
                {
                    if (File.Exists(Path.Combine(dirpath, "character.txt")) &&
                        File.Exists(Path.Combine(dirpath, "oto.ini")))
                    {
                        USinger singer = null;
                        try
                        {
                            singer = LoadSinger(dirpath);
                            singers.Add(singer.Path, singer);
                        }
                        catch { }
                    }
                }
            }
            return singers;
        }

        public static USinger GetSinger(string path, Encoding ustEncoding, Dictionary<string, USinger> loadedSingers)
        {
            var absPath = DetectSingerPath(path, ustEncoding);
            if (absPath == "") return null;
            else if (loadedSingers.ContainsKey(absPath))
            {
                if (loadedSingers[absPath] == null) return null;
                if(loadedSingers[absPath].Loaded)
                {
                    return loadedSingers[absPath];
                }
                else
                {
                    var singer = LoadSinger(absPath);
                    loadedSingers[absPath] = singer;
                    return singer;
                }
            }
            else
            {
                var singer = LoadSinger(absPath);
                loadedSingers.Add(absPath, singer);
                return singer;
            }
        }

        static string DetectSingerPath(string path, Encoding ustEncoding)
        {
            var pathEncoding = DetectSingerPathEncoding(path, ustEncoding);
            if (pathEncoding == null) return "";
            return PathManager.Inst.GetSingerAbsPath(EncodingUtil.ConvertEncoding(ustEncoding, pathEncoding, path));
        }
        public static void SaveSinger(USinger singer) {
            SaveOtos(singer);
            /*using (var writer = new StreamWriter(Path.Combine(singer.Path, "character.txt"), false, Encoding.UTF8))
            {
                if (!string.IsNullOrWhiteSpace(singer.Name)) writer.WriteLine("name=" + singer.Name);
                if (!string.IsNullOrWhiteSpace(singer.AvatarPath)) writer.WriteLine("image=" + singer.AvatarPath);
                if (!string.IsNullOrWhiteSpace(singer.Author)) writer.WriteLine("author=" + singer.Author);
                if (!string.IsNullOrWhiteSpace(singer.Website)) writer.WriteLine("web=" + singer.Website);
                if (!string.IsNullOrWhiteSpace(singer.Detail)) writer.WriteLine(singer.Detail);
            }*/
            SavePrefixMap(singer);
            SaveLyricPreset(singer);
        }
        public static USinger LoadSinger(string path)
        {
            if (!Directory.Exists(path) ||
                !File.Exists(Path.Combine(path, "character.txt")) ||
                !File.Exists(Path.Combine(path, "oto.ini"))) return null;
            
            USinger singer = new USinger();
            singer.Path = path;
            singer.FileEncoding = EncodingUtil.DetectFileEncoding(Path.Combine(singer.Path, "oto.ini"), Encoding.Default);
            singer.PathEncoding = Encoding.Default;
            string[] lines = File.ReadAllLines(Path.Combine(singer.Path, "oto.ini"), singer.FileEncoding);

            int i = 0;
            while (i < 16 && i < lines.Count())
            {
                if (lines[i].Contains("="))
                {
                    string filename = lines[i].Split(new[] { '=' })[0];
                    var detected = DetectPathEncoding(filename, singer.Path, singer.FileEncoding);
                    if (singer.PathEncoding == Encoding.Default) singer.PathEncoding = detected;
                }
                i++;
            }
            if (singer.PathEncoding == null) return null;
            
            LoadOtos(singer);

            try
            {
                lines = File.ReadAllLines(Path.Combine(singer.Path, "character.txt"), singer.FileEncoding);
            }
            catch { return null; }
            string finalstring = "";

            foreach (var line in lines){
                if (line.StartsWith("name=")) singer.Name = line.Trim().Replace("name=", "");
                else if (line.StartsWith("image="))
                {
                    string imagePath = line.Trim().Replace("image=", "");
                    singer.AvatarPath = imagePath;
                    if (!string.IsNullOrWhiteSpace(imagePath))
                    {
                        var absPath = Path.Combine(singer.Path, EncodingUtil.ConvertEncoding(singer.FileEncoding, singer.PathEncoding, imagePath));
                        if (File.Exists(absPath))
                        {
                            Uri imagepath = new Uri(absPath);
                            singer.Avatar = new System.Windows.Media.Imaging.BitmapImage(imagepath);
                            singer.Avatar.Freeze();
                        }
                    }
                }
                else if (line.StartsWith("author=")) singer.Author = line.Trim().Replace("author=", "");
                else if (line.StartsWith("web=")) singer.Website = line.Trim().Replace("web=", "");
                else finalstring += line + "\r\n";
            }
            if (File.Exists(Path.Combine(singer.Path, "readme.txt")))
            {
                using (var reader = new StreamReader(File.OpenRead(Path.Combine(singer.Path, "readme.txt"))))
                {
                    finalstring += reader.ReadToEnd();
                }
            }
            singer.Detail = finalstring;
            LoadPrefixMap(singer);
            LoadLyricPreset(singer);
            singer.Loaded = true;

            return singer;
        }

        static Encoding DetectSingerPathEncoding(string singerPath, Encoding ustEncoding)
        {
            string[] encodings = new string[] { "shift_jis", "gbk", "utf-8" };
            foreach (string encoding in encodings)
            {
                string path = EncodingUtil.ConvertEncoding(ustEncoding, Encoding.GetEncoding(encoding), singerPath);
                if (PathManager.Inst.GetSingerAbsPath(path) != "") return Encoding.GetEncoding(encoding);
            }
            return null;
        }
        
        static Encoding DetectPathEncoding(string path, string basePath, Encoding encoding)
        {
            string[] encodings = new string[] { "shift_jis", "gbk", "utf-8" };
            foreach (string enc in encodings)
            {
                string absPath = Path.Combine(basePath, EncodingUtil.ConvertEncoding(encoding, Encoding.GetEncoding(enc), path));
                if (File.Exists(absPath) || Directory.Exists(absPath)) return Encoding.GetEncoding(enc);
            }
            return null;
        }

        static void SaveOtos(USinger singer)
        {
            string path = singer.Path;
            SaveOto(path, path, singer);
            foreach (var dirpath in Directory.EnumerateDirectories(path))
                SaveOto(dirpath, path, singer);
        }

        static void LoadOtos(USinger singer)
        {
            string path = singer.Path;
            if (File.Exists(Path.Combine(path, "oto.ini"))) LoadOto(path, path, singer);
            foreach (var dirpath in Directory.EnumerateDirectories(path))
                if (File.Exists(Path.Combine(dirpath, "oto.ini"))) LoadOto(dirpath, path, singer);
        }
        static void SaveOto(string dirpath, string path, USinger singer)
        {
            string file = Path.Combine(dirpath, "oto.ini");
            string relativeDir = dirpath.Replace(path, "");
            while (relativeDir.StartsWith("\\")) relativeDir = relativeDir.Substring(1);
            var groupedAlias = singer.AliasMap.Values.GroupBy((oto) => {
                return Path.GetDirectoryName(oto.File);
            });
            if (groupedAlias.Any(grouping=>grouping.Key.Equals(relativeDir)))
            {
                var locatedAlias = groupedAlias.First(grouping => grouping.Key.Equals(relativeDir));
                using (var writer = new StreamWriter(file, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("#Charset:UTF8");
                    foreach (var oto in locatedAlias)
                    {
                        writer.WriteLine(Path.GetFileName(oto.File) + "=" + oto.Alias + "," + oto.Offset + "," + oto.Consonant + "," + oto.Cutoff + "," + oto.Preutter + "," + oto.Overlap);
                    }
                }
            }
            
        }
        static void LoadOto(string dirpath, string path, USinger singer)
        {
            string file = Path.Combine(dirpath, "oto.ini");
            string relativeDir = dirpath.Replace(path, "");
            while (relativeDir.StartsWith("\\")) relativeDir = relativeDir.Substring(1);
            string[] lines = File.ReadAllLines(file, singer.FileEncoding);
            List<string> errorLines = new List<string>();
            foreach (var line in lines)
            {
                var s = line.Split(new[] { '=' });
                if (s.Count() == 2)
                {
                    string wavfile = s[0];
                    var args = s[1].Split(new[] { ',' });
                    var alias = args[0];
                    if (string.IsNullOrWhiteSpace(alias)) alias = wavfile.Substring(0, wavfile.LastIndexOf(".wav"));
                    if (singer.AliasMap.ContainsKey(alias)) continue;
                    try
                    {
                        singer.AliasMap.Add(alias, new UOto()
                        {
                            File = Path.Combine(relativeDir, wavfile),
                            Alias = args[0],
                            Offset = double.Parse(string.IsNullOrWhiteSpace(args[1]) ? "0" : args[1]),
                            Consonant = double.Parse(string.IsNullOrWhiteSpace(args[2]) ? "0" : args[2]),
                            Cutoff = double.Parse(string.IsNullOrWhiteSpace(args[3]) ? "0" : args[3]),
                            Preutter = double.Parse(string.IsNullOrWhiteSpace(args[4]) ? "0" : args[4]),
                            Overlap = double.Parse(string.IsNullOrWhiteSpace(args[5]) ? "0" : args[5]),
                            Duration = 1
                        });
                    }
                    catch (Exception e)
                    {
                        errorLines.Add(line + " with exception " + e.GetType().Name + ":" + e.Message);
                    }
                }
            }
            if (errorLines.Count > 0)
                System.Diagnostics.Debug.WriteLine(string.Format(
                    "Oto file {0} has following errors:\n{1}", file, string.Join("\n", errorLines.ToArray())));
        }

        static void LoadPrefixMap(USinger singer)
        {
            string path = singer.Path;
            if (File.Exists(Path.Combine(path, "prefix.map"))) 
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(Path.Combine(path, "prefix.map"));
                }
                catch(IOException e)
                {
                    throw new IOException("Prefix map exists but cannot be opened for read.", e);
                }

                foreach (string line in lines)
                {
                    var s = line.Trim().Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                    if (s.Count() == 2)
                    {
                        string source = s[0];
                        string target = s[1];
                        singer.PitchMap.Add(source, target);
                    }
                }
            }
        }

        static void SavePrefixMap(USinger singer)
        {
            string path = singer.Path;
                /*using (var map = File.CreateText(Path.Combine(path, "prefix.map")))
                {

                }*/
        }

        static void SaveLyricPreset(USinger singer)
        {
            string path = singer.Path;
            using (var preset = new StreamWriter(Path.Combine(path, "lyrics-dictionary.json"), false))
            {
                JavaScriptSerializer jss = new JavaScriptSerializer();
                jss.RegisterConverters(
                    new List<JavaScriptConverter>()
                        {
                        new PresetNoteConverter(),
                        new UNoteConvertor(),
                        new UPhonemeConverter()
                        });
                StringBuilder str = new StringBuilder();
                try
                {
                    jss.Serialize(new Dictionary<string, UDictionaryNote>(singer.PresetLyricsMap), str);
                    preset.Write(str.ToString());
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                }
            }
        }
        static void LoadLyricPreset(USinger singer)
        {
            string path = Path.Combine(singer.Path, "lyrics-dictionary.json");
            if (File.Exists(path))
                using (var preset = File.OpenText(path))
                {
                    JavaScriptSerializer jss = new JavaScriptSerializer();
                    jss.RegisterConverters(
                        new List<JavaScriptConverter>()
                            {
                        new PresetNoteConverter(),
                        new UNoteConvertor(),
                        new UPhonemeConverter()
                            });
                    StringBuilder str = new StringBuilder();
                    try
                    {
                        var a = jss.Deserialize<Dictionary<string, UDictionaryNote>>(preset.ReadToEnd());
                        singer.PresetLyricsMap = new SortedDictionary<string, UDictionaryNote>(a);
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(e.ToString());
                    }
                }
        }
    }

    public class PresetNoteConverter : JavaScriptConverter
    {
        public override IEnumerable<Type> SupportedTypes => new List<Type>() { typeof(UDictionaryNote) };

        public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
        {
            var prenote = new UDictionaryNote();
            var notes = dictionary["notes"] as System.Collections.ArrayList;
            foreach (var note in notes)
            {
                UNote uNote = serializer.ConvertToType<UNote>(note);
                uNote.NoteNo = prenote.Notes.Count;
                prenote.Notes.Add(uNote.NoteNo,uNote);
            }
            foreach (var item in (Dictionary<string, object>)(dictionary["expression-processing"]))
            {
                prenote.NotesProcessing.Add(Convert.ToInt32(item.Key), (UDictionaryNote.ExpressionProcessing)item.Value);
            }
            return prenote;
        }

        public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
        {
            var realobj = obj as UDictionaryNote;
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict.Add("notes", realobj.Notes.Values);
            dict.Add("expression-processing", realobj.NotesProcessing);
            return dict;
        }
    }
}
