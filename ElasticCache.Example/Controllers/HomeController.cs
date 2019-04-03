using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ElasticCache.Example.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace ElasticCache.Example.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDistributedCache _cache;
        public HomeController(IDistributedCache cache)
        {
            _cache = cache;
        }
        public async Task<IActionResult> Index()
        {
            await _cache.SetStringAsync("Key", "Value");
            var value = await _cache.GetStringAsync("Key");
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
