using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenUtau.Core.Render
{
    struct RenderCacheItem
    {
        public string Engine;
        public CachedSound Sound;
    }

    class RenderCache
    {
        public int Capacity { set; get; }
        public int Count { get { return cache.Count; } }

        ConcurrentDictionary<uint, RenderCacheItem> cache;

        private RenderCache() { cache = new ConcurrentDictionary<uint, RenderCacheItem>(); }
        private static RenderCache _s;
        public static RenderCache Inst { get { if (_s == null) { _s = new RenderCache(); } return _s; } }

        public void Clear() { cache.Clear(); }
        public void Put(uint hash, CachedSound sound, string engine)
        {
            if (cache.ContainsKey(hash)) cache.TryRemove(hash, out _);
            RenderCacheItem item;
            item.Engine = engine;
            item.Sound = sound;
            cache.TryAdd(hash, item);
        }
        public CachedSound Get(uint hash, string engine = "")
        {
            if (cache.ContainsKey(hash))
            {
                if (engine == "" || cache[hash].Engine == engine)
                    return cache[hash].Sound;
                else return null;
            }
            else return null;
        }
        public int TotalMemSize {
            get {
                return cache.Sum(pair=>pair.Value.Sound?.MemSize ?? 0);
            }
        }
    }
}
