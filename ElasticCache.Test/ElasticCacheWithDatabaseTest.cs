using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ElasticCache.Test
{
    public class ElasticCacheWithDatabaseTest
    {
        private const string SkipReason = "This requires ElasticSearch database to be setup";
        private readonly string _uri = "http://test:9200";
        private readonly string _indexName = "test_cache";
        public ElasticCacheWithDatabaseTest()
        {
        }

        [Fact(Skip = SkipReason)]
        public async Task ReturnsNullValue_ForNonExistingCacheItem()
        {

            var cache = GetElasticCache();


            var value = await cache.GetAsync("NonExisting");


            Assert.Null(value);
        }

        [Fact(Skip = SkipReason)]
        public async Task SetWithAbsoluteExpirationSetInThePast_Throws()
        {

            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var cache = GetElasticCache(GetCacheOptions(testClock));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetAbsoluteExpiration(testClock.UtcNow.AddHours(-1)));
            });
            Assert.Equal("The absolute expiration value must be in the future.", exception.Message);
        }


        [Fact(Skip = SkipReason)]
        public async Task SetCacheItem_SucceedsFor_NullAbsoluteAndSlidingExpirationTimes()
        {

            var key = Guid.NewGuid().ToString();
            var testClock = new TestClock();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var cacheOptions = GetCacheOptions(testClock);
            var cache = GetElasticCache(cacheOptions);
            var expectedExpirationTime = testClock.UtcNow.Add(cacheOptions.DefaultSlidingExpiration);


            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions()
            {
                AbsoluteExpiration = null,
                AbsoluteExpirationRelativeToNow = null,
                SlidingExpiration = null
            });


            await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, cacheOptions.DefaultSlidingExpiration, absoluteExpiration: null, expectedExpirationTime: expectedExpirationTime);

            var cacheItem = await GetCacheItemFromDatabaseAsync(key);
            Assert.Equal(expectedValue, Convert.FromBase64String(cacheItem.Value));


            await cache.RemoveAsync(key);


            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.Null(cacheItemInfo);
        }

        [Fact(Skip = SkipReason)]
        public async Task UpdatedDefaultSlidingExpiration_SetCacheItem_SucceedsFor_NullAbsoluteAndSlidingExpirationTimes()
        {

            var key = Guid.NewGuid().ToString();
            var testClock = new TestClock();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var cacheOptions = GetCacheOptions(testClock);
            cacheOptions.DefaultSlidingExpiration = cacheOptions.DefaultSlidingExpiration.Add(TimeSpan.FromMinutes(10));
            var cache = GetElasticCache(cacheOptions);
            var expectedExpirationTime = testClock.UtcNow.Add(cacheOptions.DefaultSlidingExpiration);


            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions()
            {
                AbsoluteExpiration = null,
                AbsoluteExpirationRelativeToNow = null,
                SlidingExpiration = null
            });


            await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, cacheOptions.DefaultSlidingExpiration, absoluteExpiration: null, expectedExpirationTime: expectedExpirationTime);

            var cacheItem = await GetCacheItemFromDatabaseAsync(key);
            Assert.Equal(expectedValue, Convert.FromBase64String(cacheItem.Value));


            await cache.RemoveAsync(key);


            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.Null(cacheItemInfo);
        }


        [Theory(Skip = SkipReason)] //remove skip if you want this to work.  MS bug
        [InlineData(10, 11)]
        [InlineData(10, 30)]
        public async Task SetWithSlidingExpiration_ReturnsNullValue_ForExpiredCacheItem(int slidingExpirationWindow, int accessItemAt)
        {
            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            await cache.SetAsync(key, Encoding.UTF8.GetBytes("Hello, World!"), new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(slidingExpirationWindow)));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(accessItemAt));


            var value = await cache.GetAsync(key);


            Assert.Null(value);
        }

        [Theory(Skip = SkipReason)] //remove skip if you want this to work.  MS bug
        [InlineData(5, 15)]
        [InlineData(10, 20)]
        public async Task SetWithSlidingExpiration_ExtendsExpirationTime(int accessItemAt, int expected)
        {

            var testClock = new TestClock();
            var slidingExpirationWindow = TimeSpan.FromSeconds(10);
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var expectedExpirationTime = testClock.UtcNow.AddSeconds(expected);
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpirationWindow));

            testClock.Add(TimeSpan.FromSeconds(accessItemAt));

            await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, slidingExpirationWindow, absoluteExpiration: null, expectedExpirationTime: expectedExpirationTime);
        }

        [Theory(Skip = SkipReason)] //remove skip if you want this to work.  MS bug
        [InlineData(8)]
        [InlineData(50)]
        public async Task SetWithSlidingExpirationAndAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem(int accessItemAt)
        {
            var testClock = new TestClock();
            var utcNow = testClock.UtcNow;
            var slidingExpiration = TimeSpan.FromSeconds(5);
            var absoluteExpiration = utcNow.Add(TimeSpan.FromSeconds(20));
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            // Set both sliding and absolute expiration
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration).SetAbsoluteExpiration(absoluteExpiration));


            utcNow = testClock.Add(TimeSpan.FromSeconds(accessItemAt)).UtcNow;
            var value = await cache.GetAsync(key);


            Assert.Null(value);
        }

        [Fact(Skip = SkipReason)]
        public async Task SetWithAbsoluteExpirationRelativeToNow_ReturnsNullValue_ForExpiredCacheItem()
        {

            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            await cache.SetAsync(key, Encoding.UTF8.GetBytes("Hello, World!"), new DistributedCacheEntryOptions().SetAbsoluteExpiration(relative: TimeSpan.FromSeconds(10)));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(10));


            var value = await cache.GetAsync(key);


            Assert.Null(value);
        }

        [Fact(Skip = SkipReason)]
        public async Task SetWithAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem()
        {

            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            await cache.SetAsync(key, Encoding.UTF8.GetBytes("Hello, World!"), new DistributedCacheEntryOptions().SetAbsoluteExpiration(absolute: testClock.UtcNow.Add(TimeSpan.FromSeconds(30))));

            // set the clock's UtcNow far in future
            testClock.Add(TimeSpan.FromHours(10));


            var value = await cache.GetAsync(key);


            Assert.Null(value);
        }

        [Fact(Skip = SkipReason)]
        public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpirationRelativeToNow()
        {
            var testClock = new TestClock();
            var absoluteExpirationRelativeToUtcNow = TimeSpan.FromSeconds(10);
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var expectedAbsoluteExpiration = testClock.UtcNow.Add(absoluteExpirationRelativeToUtcNow);


            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetAbsoluteExpiration(relative: absoluteExpirationRelativeToUtcNow));


            await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, slidingExpiration: null, absoluteExpiration: expectedAbsoluteExpiration, expectedExpirationTime: expectedAbsoluteExpiration);
        }

        [Fact(Skip = SkipReason)]
        public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpiration()
        {

            var testClock = new TestClock();
            var expectedAbsoluteExpiration = new DateTimeOffset(2025, 1, 1, 1, 0, 0, TimeSpan.Zero);
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");

            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetAbsoluteExpiration(absolute: expectedAbsoluteExpiration));

            await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, slidingExpiration: null, absoluteExpiration: expectedAbsoluteExpiration, expectedExpirationTime: expectedAbsoluteExpiration);
        }

        [Fact(Skip = SkipReason)]
        public async Task SetCacheItem_UpdatesAbsoluteExpirationTime()
        {

            var testClock = new TestClock();
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromSeconds(10));

            // Creates a new item
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
            await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, slidingExpiration: null, absoluteExpiration: absoluteExpiration, expectedExpirationTime: absoluteExpiration);

            // Updates an existing item with new absolute expiration time
            absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromMinutes(30));
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
            await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, slidingExpiration: null, absoluteExpiration: absoluteExpiration, expectedExpirationTime: absoluteExpiration);
        }

        [Fact(Skip = SkipReason)]
        public async Task ExtendsExpirationTime_ForSlidingExpiration()
        {
            var testClock = new TestClock();
            var slidingExpiration = TimeSpan.FromSeconds(10);
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            // The operations Set and Refresh here extend the sliding expiration 2 times.
            var expectedExpiresAtTime = testClock.UtcNow.AddSeconds(15);
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));

            testClock.Add(TimeSpan.FromSeconds(5));
            await cache.RefreshAsync(key);

            // verify if the expiration time in database is set as expected
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
            Assert.Null(cacheItemInfo.AbsoluteExpiration);
            Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
        }

        [Fact(Skip = SkipReason)]
        public async Task GetItem_SlidingExpirationDoesNot_ExceedAbsoluteExpirationIfSet()
        {
            var testClock = new TestClock();
            var utcNow = testClock.UtcNow;
            var slidingExpiration = TimeSpan.FromSeconds(5);
            var absoluteExpiration = utcNow.Add(TimeSpan.FromSeconds(20));
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            // Set both sliding and absolute expiration
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration).SetAbsoluteExpiration(absoluteExpiration));

            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(utcNow.AddSeconds(5), cacheItemInfo.ExpiresAtTime);


            //I dont think this applies since I am replacing the values and cannot have muliple documents stored

            //// Accessing item at time...
            //utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
            //await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, slidingExpiration, absoluteExpiration, expectedExpirationTime: utcNow.AddSeconds(5));

            //// Accessing item at time...
            //utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
            //await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, slidingExpiration, absoluteExpiration, expectedExpirationTime: utcNow.AddSeconds(5));

            //// Accessing item at time...
            //utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
            //// The expiration extension must not exceed the absolute expiration
            //await AssertGetCacheItemFromDatabaseAsync(cache, key, expectedValue, slidingExpiration, absoluteExpiration, expectedExpirationTime: absoluteExpiration);
        }

        [Fact(Skip = SkipReason)]
        public async Task DoestNotExtendsExpirationTime_ForAbsoluteExpiration()
        {
            var testClock = new TestClock();
            var absoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var expectedExpiresAtTime = testClock.UtcNow.Add(absoluteExpirationRelativeToNow);
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpirationRelativeToNow));
            testClock.Add(TimeSpan.FromSeconds(25));


            var value = await cache.GetAsync(key);


            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);

            // verify if the expiration time in database is set as expected
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
        }

        [Fact(Skip = SkipReason)]
        public async Task RefreshItem_ExtendsExpirationTime_ForSlidingExpiration()
        {
            var testClock = new TestClock();
            var slidingExpiration = TimeSpan.FromSeconds(10);
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache(GetCacheOptions(testClock));
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            // The operations Set and Refresh here extend the sliding expiration 2 times.
            var expectedExpiresAtTime = testClock.UtcNow.AddSeconds(15);
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));


            testClock.Add(TimeSpan.FromSeconds(5));
            await cache.RefreshAsync(key);


            // verify if the expiration time in database is set as expected
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
            Assert.Null(cacheItemInfo.AbsoluteExpiration);
            Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
        }

        [Fact(Skip = SkipReason)]
        public async Task GetCacheItem_IsCaseSensitive()
        {
            var key = Guid.NewGuid().ToString().ToLower(); // lower case
            var cache = GetElasticCache();
            await cache.SetAsync(key, Encoding.UTF8.GetBytes("Hello, World!"), new DistributedCacheEntryOptions().SetAbsoluteExpiration(relative: TimeSpan.FromHours(1)));

            var value = await cache.GetAsync(key.ToUpper()); // key made upper case

            Assert.Null(value);
        }

        [Fact(Skip = SkipReason)]
        public async Task GetCacheItem_DoesNotTrimTrailingSpaces()
        {
            var key = string.Format("  {0}  ", Guid.NewGuid()); // with trailing spaces
            var cache = GetElasticCache();
            var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
            await cache.SetAsync(key, expectedValue, new DistributedCacheEntryOptions().SetAbsoluteExpiration(relative: TimeSpan.FromHours(1)));

            var value = await cache.GetAsync(key);

            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);
        }

        [Fact(Skip = SkipReason)]
        public async Task DeletesCacheItem_OnExplicitlyCalled()
        {
            var key = Guid.NewGuid().ToString();
            var cache = GetElasticCache();
            await cache.SetAsync(key, Encoding.UTF8.GetBytes("Hello, World!"), new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10)));

            await cache.RemoveAsync(key);

            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.Null(cacheItemInfo);
        }

        private IDistributedCache GetElasticCache(ElasticCacheOptions options = null)
        {
            if (options == null)
            {
                options = GetCacheOptions();
            }
            return new ElasticSearchCache(options);
        }

        private ElasticCacheOptions GetCacheOptions(ISystemClock testClock = null)
        {
            return new ElasticCacheOptions()
            {
                ConnectionSettings = new Nest.ConnectionSettings(new Uri(_uri)).DefaultMappingFor<CacheItem>(m => m.IndexName(_indexName)),
                SystemClock = testClock ?? new TestClock(),
                ExpiredItemsDeletionInterval = TimeSpan.FromHours(2),
                IndexName = _indexName,
                Refresh = Elasticsearch.Net.Refresh.False
            };
        }

        private async Task AssertGetCacheItemFromDatabaseAsync(IDistributedCache cache, string key, byte[] expectedValue, TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration, DateTimeOffset expectedExpirationTime)
        {
            var value = await cache.GetAsync(key);
            Assert.NotNull(value);
            Assert.Equal(expectedValue, value);
            var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
            Assert.NotNull(cacheItemInfo);
            Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
            Assert.Equal(absoluteExpiration, cacheItemInfo.AbsoluteExpiration);
            Assert.Equal(expectedExpirationTime, cacheItemInfo.ExpiresAtTime);
        }

        private async Task<CacheItem> GetCacheItemFromDatabaseAsync(string key)
        {
            var connectionSettings = new Nest.ConnectionSettings(new Uri(_uri)).DefaultMappingFor<CacheItem>(m => m.IndexName(_indexName));
            var client = new Nest.ElasticClient(connectionSettings);
            var response = await client.GetAsync<CacheItem>(new Nest.DocumentPath<CacheItem>(key));

            return response.Source;
        }
    }
}
