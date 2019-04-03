using ElasticCache;
using Microsoft.Extensions.Caching.Distributed;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ElasticCacheServicesExtensions
    {
        public static IServiceCollection AddDistributedElasticCache(this IServiceCollection services, Action<ElasticCacheOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddOptions();
            services.AddSingleton<IDistributedCache, ElasticSearchCache>();
            services.Configure(setupAction);

            return services;
        }

    }
}
