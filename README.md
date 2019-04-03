# ElasticCache
An IDistributedCache implementation for Elastic Search

Microsoft Provides an In-Memory Cache and a Sql Server Cache.  This library will store the cached data in Elastic Search.

[![elasticcache MyGet Build Status](https://www.myget.org/BuildSource/Badge/elasticcache?identifier=cb4de5c1-4fec-4945-9403-a4928e81f636)](https://www.myget.org/)


# Dependency Injection
This will add the service to the IServiceCollection and make available to be injected.  The two required parameters are set below.  IndexName is similar to a table name.
```
services.AddDistributedElasticCache(c =>
{
   c.ConnectionSettings = new Nest.ConnectionSettings(new Uri("http://localhost:9200"));
   c.IndexName = "test_cache";
});
```

# Usage
This example simply injects the cache service into the controller, caches a string value named "Key", and then retrieves it.
```
public class TestController
{
  private readonly IDistributedCache _cache;
  public TestController(IDistributedCache cache)
  {
    _cache = cache;
  }
  public async Task<IActionResult> Index()
  {
    await _cache.SetStringAsync("Key", "Value");
    var value = await _cache.GetStringAsync("Key");
    return View();
  }
}
```

# Extensions for object caching
You may want to utilize some extension methods created to easily serialize an object to/from the cache.
```
var testData = _cache.TryGet("test1", () => { return new TestData(); });

    public class TestData
    {
        public int test1 { get; set; } = 1;
        public decimal test2 { get; set; } = 1231.001m;
        public string test3 { get; set; } = "test 333";
        public DateTime test4 { get; set; } = DateTime.Now;
    }
```
   
# Optional Compression
Serializing large objects may be slow over the network so it may be best to compress the data.  The setting for MinLengthCompress is used to only compress data that is large enough to need it.
```
services.AddDistributedElasticCache(c =>
{
   c.ConnectionSettings = new Nest.ConnectionSettings(new Uri("http://localhost:9200"));
   c.IndexName = "test_cache";
   c.Compress = true;
   c.MinLengthCompress = 2048;
});
```

# More Information
[Microsoft IDistributedCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-2.2)
