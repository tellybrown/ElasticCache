using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Linq;
using Xunit;

namespace ElasticCache.Test
{
    public class ElasticCacheServicesExtensionsTest
    {
        [Fact]
        public void AddDistributedElasticCache_AddsAsSingleRegistrationService()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddDistributedElasticCache(c =>
            {
                c.ConnectionSettings = new Nest.ConnectionSettings(new Uri("http://test.com"));
                c.IndexName = "test";
                c.Refresh = Elasticsearch.Net.Refresh.False;
            });

            var serviceDescriptor = services.FirstOrDefault(desc => desc.ServiceType == typeof(IDistributedCache));
            Assert.Equal(typeof(IDistributedCache), serviceDescriptor.ServiceType);
            Assert.Equal(typeof(ElasticSearchCache), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
        }

        [Fact]
        public void AddDistributedElasticCache_ReplacesPreviouslyUserRegisteredServices()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddScoped(typeof(IDistributedCache), sp => Mock.Of<IDistributedCache>());

            services.AddDistributedElasticCache(c =>
            {
                c.ConnectionSettings = new Nest.ConnectionSettings(new Uri("http://test.com"));
                c.IndexName = "test";
                c.Refresh = Elasticsearch.Net.Refresh.False;
            });


            var serviceProvider = services.BuildServiceProvider();

            var distributedCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(IDistributedCache));

            Assert.NotNull(distributedCache);
            Assert.Equal(ServiceLifetime.Scoped, distributedCache.Lifetime);
            Assert.IsType<ElasticSearchCache>(serviceProvider.GetRequiredService<IDistributedCache>());
        }

        [Fact]
        public void AddDistributedElasticCache_allows_chaining()
        {
            IServiceCollection services = new ServiceCollection();

            Assert.Same(services, services.AddDistributedElasticCache(_ => { }));
        }
    }
}
