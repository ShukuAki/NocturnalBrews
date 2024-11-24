using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NocturnalBrews.Models;
using System.Diagnostics;

namespace NocturnalBrews.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly NocturnalBrewsContext _context;
        public HomeController(ILogger<HomeController> logger , NocturnalBrewsContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // Get distinct products by name
            var products = _context.ProductsTbs  // Assuming your DbSet is named Products
                .GroupBy(p => p.ProductName)
                .Select(g => g.First())
                .ToList();

            return View(products);
        }


        // GET: Products/GetSizes
        [HttpGet]
        public JsonResult GetSizes(string productName)
        {
            var sizes = _context.ProductsTbs
                .Where(p => p.ProductName == productName)
                .Select(p => new { p.Size, p.Price })
                .ToList();

            return Json(sizes);
        }


        [HttpGet]
        public async Task<IActionResult> GetPrice(string productName, string size)
        {
            var product = await _context.ProductsTbs
                .Where(p => p.ProductName == productName && p.Size == size)
                .Select(p => new { price = p.Price })  // Price is now returned as int
                .FirstOrDefaultAsync();

            if (product == null)
                return NotFound();

            return Json(product);
        }


        //Generated Functions
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
