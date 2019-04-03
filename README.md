# ElasticCache
An IDistributedCache implementation for Elastic Search

Microsoft Provides an In-Memory Cache and a Sql Server Cache.  This library will store the cached data in Elastic Search.


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
