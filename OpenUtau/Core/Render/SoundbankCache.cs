using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OpenUtau.Core.USTx;
using System.Security.Cryptography;

namespace OpenUtau.Core.Render
{
    public class SoundbankCache
    {
        public static readonly string CachePath = Path.Combine(DocManager.CachePath, "Soundbank");
        static ConcurrentDictionary<string, string> SingerMap = new ConcurrentDictionary<string, string>();
        static ConcurrentDictionary<string, string> SoundMap = new ConcurrentDictionary<string, string>();
        static readonly SHA256 Sha256 = SHA256.Create();

        public static void MakeSingerCache(USinger singer)
        {
            if (singer == null || !singer.Loaded) return;
            if (CachedSingers.Contains(singer)) return;
            string enpath;
            if (SingerMap.ContainsKey(singer.Path))
            {
                enpath = SingerMap[singer.Path];
            }
            else
            {
                enpath = BitConverter.ToString(Sha256.ComputeHash(Encoding.Unicode.GetBytes(singer.Path))).Replace("-", "");
                SingerMap.TryAdd(singer.Path, enpath);
            }
            string root = Path.Combine(CachePath, enpath);
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);
            ScanFiles(singer.Path, singer.Path, 2);
            CachedSingers.Add(singer);
        }
        public static string GetSoundCachePath(UOto oto, USinger singer)
        {
            return GetSoundCachePath(Path.Combine(singer.Path, oto.File), singer);
        }
        public static string GetSoundCachePath(RenderItem oto, USinger singer)
        {
            return GetSoundCachePath(oto.RawFile, singer);
        }

        public static string GetSoundCachePath(string file, USinger singer)
        {
            return file;
            if (singer == null || !singer.Loaded || !File.Exists(file) || !SoundMap.ContainsKey(file)) return file;
            if (!SingerMap.ContainsKey(singer.Path)) MakeSingerCache(singer);
            string root = Path.Combine(CachePath, SingerMap[singer.Path]);
            string enname = SoundMap[file];
            string path = Path.Combine(root, enname + Path.GetExtension(file));
            if (!File.Exists(path))
            {
                return file;
                var src = new FileInfo(file);
                var dest = new FileInfo(path);
                src.CopyTo(path, true);
                dest.CreationTime = src.CreationTime;
                dest.LastWriteTime = src.LastWriteTime;
                dest.LastAccessTime = src.LastAccessTime;
            }
            return path;
        }

        static void ScanFiles(string dir, string parent, int recurse)
        {
            if (!SingerMap.TryGetValue(parent, out string enpath)) return;
            string root = Path.Combine(CachePath, enpath);
            string rel = dir.Replace(parent, "");
            string relen = string.IsNullOrEmpty(rel) ? "" : BitConverter.ToString(Sha256.ComputeHash(Encoding.Unicode.GetBytes(rel))).Replace("-", "");
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                string enname;
                string combn = string.IsNullOrEmpty(rel) ? file : Path.Combine(rel, file);
                if (SoundMap.ContainsKey(combn)) {
                    enname = SoundMap[combn];
                }
                else
                {
                    if (Path.GetExtension(file) == ".frq")
                    {
                        enname = BitConverter.ToString(Sha256.ComputeHash(Encoding.Unicode.GetBytes(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file))))).Replace("-", "") + Path.GetExtension(Path.GetFileNameWithoutExtension(file));
                    }
                    else if(Path.GetFileName(file) == "desc.mrq")
                    {
                        enname = Path.GetFileNameWithoutExtension(file);
                    }else
                    {
                        enname = BitConverter.ToString(Sha256.ComputeHash(Encoding.Unicode.GetBytes(Path.GetFileNameWithoutExtension(file)))).Replace("-", "");
                    }
                    enname = string.IsNullOrEmpty(relen) ? enname : Path.Combine(relen, enname);
                    SoundMap.TryAdd(combn, enname);
                }
                string path = Path.Combine(root, enname + Path.GetExtension(file));
                var src = new FileInfo(combn);
                var dest = new FileInfo(path);
                if (File.Exists(path))
                {
                    if (dest.CreationTime == src.CreationTime && dest.LastWriteTime == src.LastWriteTime && dest.LastAccessTime == src.LastAccessTime) continue;
                    byte[] a, b;
                    using (var a1 = src.OpenRead())
                    {
                        a = Sha256.ComputeHash(a1);
                    }
                    using (var b1 = dest.OpenRead())
                    {
                        b = Sha256.ComputeHash(b1);
                    }
                    if (Array.Equals(a, b)) continue;
                }
                if (!Directory.Exists(Path.GetDirectoryName(path))) Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(file, path, true);
                dest.CreationTime = src.CreationTime;
                dest.LastWriteTime = src.LastWriteTime;
                dest.LastAccessTime = src.LastAccessTime;
            }
            if (recurse > 0)
                foreach (var dir1 in Directory.EnumerateDirectories(dir))
                {
                    ScanFiles(dir1, parent, --recurse);
                }
        }

        static HashSet<USinger> CachedSingers = new HashSet<USinger>();

        public static void FlushCachedSingers() {
            CachedSingers.Clear();
        }
    }
}
