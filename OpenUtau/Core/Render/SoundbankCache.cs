using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OpenUtau.Core.USTx;

namespace OpenUtau.Core.Render
{
    public class SoundbankCache
    {
        public static readonly string CachePath = Path.Combine(DocManager.CachePath, "Soundbank");

        public static void MakeSingerCache(USinger singer) {
            if (singer == null || !singer.Loaded) return;
            var sha = System.Security.Cryptography.SHA256.Create();
            string root = Path.Combine(CachePath, BitConverter.ToString(sha.ComputeHash(Encoding.Unicode.GetBytes(singer.Path))).Replace("-",""));
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);
            foreach (var file in Directory.EnumerateFiles(singer.Path))
            {
                string enname = BitConverter.ToString(sha.ComputeHash(Encoding.Unicode.GetBytes(Path.GetFileNameWithoutExtension(file)))).Replace("-", "");
                string path = Path.Combine(root, enname + Path.GetExtension(file));
                if (File.Exists(path)) {
                    byte[] a, b;
                    using (var a1 = File.OpenRead(file))
                    {
                        a = sha.ComputeHash(a1);
                    }
                    using (var b1 = File.OpenRead(path))
                    {
                        b = sha.ComputeHash(b1);
                    }
                    if (Array.Equals(a, b)) continue;
                }
                File.Copy(file, path, true);
            }
            foreach (var dir in Directory.EnumerateDirectories(singer.Path))
            {
                string root1 = Path.Combine(root, Path.GetDirectoryName(dir));
                if (!Directory.Exists(root1)) Directory.CreateDirectory(root1);
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    string enname = BitConverter.ToString(sha.ComputeHash(Encoding.Unicode.GetBytes(Path.GetFileNameWithoutExtension(file)))).Replace("-", "");
                    string path = Path.Combine(root1, enname + Path.GetExtension(file));
                    if (File.Exists(path))
                    {
                        byte[] a, b;
                        using (var a1 = File.OpenRead(file))
                        {
                            a = sha.ComputeHash(a1);
                        }
                        using (var b1 = File.OpenRead(path))
                        {
                            b = sha.ComputeHash(b1);
                        }
                        if (Array.Equals(a, b)) continue;
                    }
                    File.Copy(file, path, true);
                }
            }
        }
        public static string GetSoundCachePath(UOto oto, USinger singer)
        {
            return GetSoundCachePath(Path.Combine(singer.Path, oto.File), singer);
        }
        public static string GetSoundCachePath(RenderItem oto, USinger singer) {
            return GetSoundCachePath(oto.RawFile, singer);
        }

        public static string GetSoundCachePath(string file, USinger singer)
        {
            if (singer == null || !singer.Loaded || !File.Exists(file)) return file;
            var sha = System.Security.Cryptography.SHA256.Create();
            string root = Path.Combine(CachePath, BitConverter.ToString(sha.ComputeHash(Encoding.Unicode.GetBytes(singer.Path))).Replace("-", ""));
            if (!Directory.Exists(root)) MakeSingerCache(singer);
            string enname = BitConverter.ToString(sha.ComputeHash(Encoding.Unicode.GetBytes(Path.GetFileNameWithoutExtension(file)))).Replace("-", "");
            string path = Path.Combine(root, enname + Path.GetExtension(file));
            return path;
        }
    }
}
