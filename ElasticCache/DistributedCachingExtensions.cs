using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Microsoft.Extensions.Caching.Distributed
{
    public static class ElasticCacheServicesExtensions
    {
        public static async Task<T> TryGetAsync<T>(this IDistributedCache distributedCache, string name, Func<Task<T>> func, CancellationToken token = default(CancellationToken))
        {
            var t = await distributedCache.GetAsync<T>(name);

            if (t == null)
            {
                t = await func();
                await distributedCache.SetAsync(name, t, token);
            }
            return t;
        }
        public static async Task SetAsync<T>(this IDistributedCache distributedCache, string name, T t, CancellationToken token = default(CancellationToken))
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, t);
                var bytes = stream.ToArray();
                await distributedCache.SetAsync(name, bytes, token);
            }
        }
        public static async Task<T> GetAsync<T>(this IDistributedCache distributedCache, string name, CancellationToken token = default(CancellationToken))
        {
            T ret = default(T);
            var serializer = new XmlSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                var bytes = await distributedCache.GetAsync(name, token);
                if (bytes != null)
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                    stream.Position = 0;
                    ret = (T)serializer.Deserialize(stream);
                }
            }
            return ret;
        }

        public static T TryGet<T>(this IDistributedCache distributedCache, string name, Func<T> func)
        {
            var t = distributedCache.Get<T>(name);

            if (t == null)
            {
                t = func();
                distributedCache.Set(name, t);
            }
            return t;
        }
        public static void Set<T>(this IDistributedCache distributedCache, string name, T t)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, t);
                var bytes = stream.ToArray();
                distributedCache.Set(name, bytes);
            }
        }
        public static T Get<T>(this IDistributedCache distributedCache, string name)
        {
            T ret = default(T);
            var bytes = distributedCache.Get(name);
            if (bytes != null)
            {
                var serializer = new XmlSerializer(typeof(T));
                using (var stream = new MemoryStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Position = 0;
                    ret = (T)serializer.Deserialize(stream);
                }
            }
            return ret;
        }
    }
}
