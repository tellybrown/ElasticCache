using Elasticsearch.Net;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Nest;
using System;

namespace ElasticCache
{ 
    public class ElasticCacheOptions: IOptions<ElasticCacheOptions>
    {
        /// <summary>
        /// An abstraction to represent the clock of a machine in order to enable unit testing.
        /// </summary>
        public ISystemClock SystemClock { get; set; }

        /// <summary>
        /// The periodic interval to scan and delete expired items in the cache. Default is 30 minutes.
        /// </summary>
        public TimeSpan? ExpiredItemsDeletionInterval { get; set; }

        /// <summary>
        /// The uri to elasticSearch
        /// </summary>
        public ConnectionSettings ConnectionSettings { get; set; }

        /// <summary>
        /// The index name to store the data
        /// </summary>
        public string IndexName { get; set; }

        /// <summary>
        /// The default sliding expiration set for a cache entry if neither Absolute or SlidingExpiration has been set explicitly.
        /// By default, its 20 minutes.
        /// </summary>
        public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(20);

        /// <summary>
        /// If `true` then refresh the affected shards to make this operation visible to search, 
        /// if `wait_for` then wait for a refresh to make this operation visible to search, 
        /// if `false` (the default) then do nothing with refreshes.
        /// </summary>
        public Refresh Refresh { get; set; } = Refresh.False;

        ElasticCacheOptions IOptions<ElasticCacheOptions>.Value
        {
            get
            {
                return this;
            }
        }
    }
}