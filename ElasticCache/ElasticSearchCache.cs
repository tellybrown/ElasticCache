using Elasticsearch.Net;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Nest;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ElasticCache
{
    public class ElasticSearchCache : IDistributedCache
    {
        private static readonly TimeSpan MinimumExpiredItemsDeletionInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30);

        private readonly ISystemClock _systemClock;
        private readonly TimeSpan _expiredItemsDeletionInterval;
        private DateTimeOffset _lastExpirationScan;
        private readonly Action _deleteExpiredCachedItemsDelegate;
        private readonly TimeSpan _defaultSlidingExpiration;
        private readonly IElasticClient _client;
        private readonly Refresh _refresh;
        private readonly bool _compress;
        private readonly int _minLengthCompress;


        public ElasticSearchCache(IOptions<ElasticCacheOptions> options)
        {
            var optionsValue = options.Value;

            _refresh = optionsValue.Refresh;
            _compress = optionsValue.Compress;
            _minLengthCompress = optionsValue.MinLengthCompress;

            if (optionsValue.ConnectionSettings == null)
            {
                throw new ArgumentException(
                    $"{nameof(ElasticCacheOptions.ConnectionSettings)} cannot be null.");
            }
            if (string.IsNullOrEmpty((string)optionsValue.IndexName))
            {
                throw new ArgumentException(
                    $"{nameof(ElasticCacheOptions.IndexName)} cannot be empty or null.");
            }
            if (optionsValue.ExpiredItemsDeletionInterval.HasValue &&
                optionsValue.ExpiredItemsDeletionInterval.Value < MinimumExpiredItemsDeletionInterval)
            {
                throw new ArgumentException(
                    $"{nameof(ElasticCacheOptions.ExpiredItemsDeletionInterval)} cannot be less than the minimum " +
                    $"value of {MinimumExpiredItemsDeletionInterval.TotalMinutes} minutes.");
            }
            if (optionsValue.DefaultSlidingExpiration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(optionsValue.DefaultSlidingExpiration),
                    optionsValue.DefaultSlidingExpiration,
                    "The sliding expiration value must be positive.");
            }

            _systemClock = optionsValue.SystemClock ?? new SystemClock();
            _expiredItemsDeletionInterval = optionsValue.ExpiredItemsDeletionInterval ?? DefaultExpiredItemsDeletionInterval;
            _deleteExpiredCachedItemsDelegate = DeleteExpiredCacheItems;
            _defaultSlidingExpiration = optionsValue.DefaultSlidingExpiration;

            _client = new ElasticClient(optionsValue.ConnectionSettings.DefaultMappingFor<CacheItem>(m => m.IndexName(optionsValue.IndexName)));
        }


        public byte[] Get(string key)
        {
            var cacheItem = GetCacheItem(key);

            ScanForExpiredItemsIfRequired();

            return cacheItem != null ? Decompress(cacheItem.Value) : null;
        }

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            token.ThrowIfCancellationRequested();

            var cacheItem = await GetCacheItemAsync(key);

            ScanForExpiredItemsIfRequired();

            return cacheItem != null ? Decompress(cacheItem.Value) : null;
        }

        public void Refresh(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var cacheItem = GetCacheItem(key);

            ScanForExpiredItemsIfRequired();

            if (cacheItem != null)
            {
                UpdateExpireTime(cacheItem);
                _client.Index<CacheItem>(cacheItem, i => i.Id(cacheItem.Id).Refresh(_refresh));
            }
        }

        public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            token.ThrowIfCancellationRequested();

            var cacheItem = await GetCacheItemAsync(key);

            ScanForExpiredItemsIfRequired();

            if (cacheItem != null)
            {
                UpdateExpireTime(cacheItem);
                await _client.IndexAsync<CacheItem>(cacheItem, i => i.Id(cacheItem.Id).Refresh(_refresh));
            }
        }
        public void Remove(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _client.Delete<CacheItem>(key);

            ScanForExpiredItemsIfRequired();
        }

        public async Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            token.ThrowIfCancellationRequested();

            await _client.DeleteAsync<CacheItem>(key);

            ScanForExpiredItemsIfRequired();
        }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            GetOptions(ref options);

            var cacheItem = CreateCacheItem(key, value, options);
            _client.Index<CacheItem>(cacheItem, i => i.Id(cacheItem.Id).Refresh(_refresh));

            ScanForExpiredItemsIfRequired();
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            token.ThrowIfCancellationRequested();

            GetOptions(ref options);

            var cacheItem = CreateCacheItem(key, value, options);
            await _client.IndexAsync<CacheItem>(cacheItem, i => i.Id(cacheItem.Id).Refresh(_refresh));

            ScanForExpiredItemsIfRequired();
        }

        // Called by multiple actions to see how long it's been since we last checked for expired items.
        // If sufficient time has elapsed then a scan is initiated on a background task.
        private void ScanForExpiredItemsIfRequired()
        {
            var utcNow = _systemClock.UtcNow;
            // TODO: Multiple threads could trigger this scan which leads to multiple calls to database.
            if ((utcNow - _lastExpirationScan) > _expiredItemsDeletionInterval)
            {
                _lastExpirationScan = utcNow;
                Task.Run(_deleteExpiredCachedItemsDelegate);
            }
        }

        private void DeleteExpiredCacheItems()
        {
            var utcNow = _systemClock.UtcNow;
            _client.DeleteByQuery<CacheItem>(q => q.Query(rq => rq.DateRange(dr => dr.LessThan(utcNow.UtcDateTime))));
        }

        private void GetOptions(ref DistributedCacheEntryOptions options)
        {
            if (!options.AbsoluteExpiration.HasValue && !options.AbsoluteExpirationRelativeToNow.HasValue && !options.SlidingExpiration.HasValue)
            {
                options = new DistributedCacheEntryOptions()
                {
                    SlidingExpiration = _defaultSlidingExpiration
                };
            }
        }
        private CacheItem CreateCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            GetOptions(ref options);

            var utcNow = _systemClock.UtcNow;

            var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpiration);

            var cacheItem = new CacheItem()
            {
                Id = key,
                Value = GetValue(value),
                AbsoluteExpiration = absoluteExpiration,
                SlidingExpirationInSeconds = options.SlidingExpiration,
                ExpiresAtTime = GetExpireTime(options, utcNow, absoluteExpiration)
            };
            //UpdateExpireTime(cacheItem);
            return cacheItem;
        }
        private string GetValue(byte[] value)
        {
            if (_compress && value.Length >= _minLengthCompress)
            {
                return Compress(value);
            }
            else
            {
                return Convert.ToBase64String(value);
            }
        }
        private DateTimeOffset GetExpireTime(DistributedCacheEntryOptions options, DateTimeOffset utcNow, DateTimeOffset? absoluteExpiration)
        {
            if (options.SlidingExpiration == null)
            {
                return absoluteExpiration.Value;
            }
            else
            {
                return utcNow.Add(options.SlidingExpiration.Value);
            }
        }
        private void UpdateExpireTime(CacheItem cacheItem)
        {
            var utcNow = _systemClock.UtcNow;
            if (utcNow <= cacheItem.ExpiresAtTime && cacheItem.SlidingExpirationInSeconds != null && (cacheItem.AbsoluteExpiration == null || cacheItem.AbsoluteExpiration != cacheItem.ExpiresAtTime))
            {
                if (cacheItem.AbsoluteExpiration.HasValue && utcNow.Subtract(cacheItem.AbsoluteExpiration.Value) < cacheItem.SlidingExpirationInSeconds)
                {
                    cacheItem.ExpiresAtTime = cacheItem.AbsoluteExpiration.Value;
                }
                else
                {
                    cacheItem.ExpiresAtTime = utcNow.Add(cacheItem.SlidingExpirationInSeconds.Value);
                }
            }
        }
        private void ValidateOptions(TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
        {
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            {
                throw new InvalidOperationException("Either absolute or sliding expiration needs to be provided.");
            }
        }
        private DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
        {
            // calculate absolute expiration
            DateTimeOffset? absoluteExpiration = null;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration.Value <= utcNow)
                {
                    throw new InvalidOperationException("The absolute expiration value must be in the future.");
                }

                absoluteExpiration = options.AbsoluteExpiration.Value;
            }
            return absoluteExpiration;
        }
        private async Task<CacheItem> GetCacheItemAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            token.ThrowIfCancellationRequested();

            var response = await _client.GetAsync<CacheItem>(new DocumentPath<CacheItem>(key));

            return CheckExpired(response);
        }
        private CacheItem GetCacheItem(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var response = _client.Get<CacheItem>(new DocumentPath<CacheItem>(key));

            return CheckExpired(response);
        }
        private CacheItem CheckExpired(IGetResponse<CacheItem> response)
        {
            if (response.Source == null || response.Source.ExpiresAtTime < _systemClock.UtcNow)
            {
                return null;
            }
            else
            {
                var cacheItem = response.Source;
                var expires = cacheItem.ExpiresAtTime;
                UpdateExpireTime(cacheItem);

                if (expires != cacheItem.ExpiresAtTime)
                {
                    _client.Index<CacheItem>(cacheItem, i => i.Id(cacheItem.Id).Refresh(_refresh));
                }

                return cacheItem;
            }
        }
        private readonly string _header = "[Compress]";
        private string Compress(byte[] bytes)
        {
            using (var stream = new MemoryStream())
            {
                using (GZipStream compressionStream = new GZipStream(stream, CompressionMode.Compress))
                {
                    compressionStream.Write(bytes, 0, bytes.Length);
                }

                return _header + Convert.ToBase64String(stream.ToArray());
            }
        }
        private byte[] Decompress(string value)
        {
            if (value.StartsWith(_header))
            {
                var bytes = Convert.FromBase64String(value.Substring(_header.Length));

                using (var stream = new MemoryStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Position = 0;

                    using (GZipStream compressionStream = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        byte[] buffer = new byte[1024];
                        int nRead;
                        using (var outputStream = new MemoryStream())
                        {
                            while ((nRead = compressionStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                outputStream.Write(buffer, 0, nRead);
                            }
                            return outputStream.ToArray();
                        }
                    }
                }
            }
            else
            {
                return Convert.FromBase64String(value);
            }
        }
    }
}
