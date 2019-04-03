using System;
using System.Collections.Generic;
using System.Text;

namespace ElasticCache
{
    public class CacheItem
    {
        public string Id { get; set; }
        public string Value { get; set; }
        public DateTimeOffset ExpiresAtTime { get; set; }
        public TimeSpan? SlidingExpirationInSeconds { get; set; }
        public DateTimeOffset? AbsoluteExpiration { get; set; }
    }
}
