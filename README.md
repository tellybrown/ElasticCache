# ElasticCache
An IDistributedCache implementation for Elastic Search

Microsoft Provides an In-Memory Cache and a Sql Server Cache.  This library will store the cached data in Elastic Search.

[![elasticcache MyGet Build Status](https://www.myget.org/BuildSource/Badge/elasticcache?identifier=cb4de5c1-4fec-4945-9403-a4928e81f636)](https://www.myget.org/)


# Dependency Injection
```
services.AddDistributedElasticCache(c =>
{
   c.ConnectionSettings = new Nest.ConnectionSettings(new Uri("http://localhost:9200"));
   c.IndexName = "test_cache";
});
```

# Usage
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


# More Information
[Microsoft IDistributedCache](https://docs.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-2.2)
