using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NocturnalBrews.Models;
using System.Diagnostics;
using JsonException = Newtonsoft.Json.JsonException;

namespace NocturnalBrews.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly NocturnalBrewsContext _context;
        public HomeController(ILogger<HomeController> logger, NocturnalBrewsContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // Existing code for products
            var products = _context.ProductsTbs
                .GroupBy(p => p.ProductName)
                .Select(g => g.First())
                .ToList();

            // Get orders and process them in memory
            var orders = _context.OrdersTbs
                .Where(o => o.ProductsArray != null)
                .ToList();

            var productCounts = new Dictionary<string, int>();

            foreach (var order in orders)
            {
                try
                {
                    var orderItems = JsonConvert.DeserializeObject<List<OrderItem>>(order.ProductsArray);
                    if (orderItems == null) continue;

                    foreach (var item in orderItems)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.ProductName))
                        {
                            var productName = item.ProductName.Trim();
                            if (!productCounts.ContainsKey(productName))
                                productCounts[productName] = 0;
                            productCounts[productName]++;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError($"Error deserializing order {order.OrderId}: {ex.Message}");
                    continue;
                }
            }

            // Debug logging
            _logger.LogInformation($"Found {productCounts.Count} unique products");
            foreach (var pc in productCounts)
            {
                _logger.LogInformation($"Product: {pc.Key}, Count: {pc.Value}");
            }

            var bestSellers = productCounts
                .Select(pc => new
                {
                    ProductName = pc.Key,
                    OrderCount = pc.Value
                })
                .OrderByDescending(x => x.OrderCount)
                .Take(10) //to view this as top 5 or top 10 best sellers edit this value
                .ToList();

            _logger.LogInformation($"Best sellers count: {bestSellers.Count}");
            ViewBag.BestSellers = bestSellers;

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

        [HttpPost]
        public IActionResult SubmitTransaction([FromBody] TransactionViewModel transaction)
        {
            try
            {
                // Create new order
                var order = new OrdersTb
                {
                    Price = (int)Math.Round(transaction.TotalAmount), // Convert decimal to int
                    Mop = transaction.PaymentMode,
                    Change = (int)Math.Round(transaction.Change), // Convert decimal to int
                    ProductsArray = System.Text.Json.JsonSerializer.Serialize(transaction.Products),
                    Total = (int)Math.Round(transaction.TotalAmount), // Convert decimal to int
                    OrderDateTime = DateTime.Now
                };

                _context.OrdersTbs.Add(order);
                _context.SaveChanges();

                return Json(new { success = true, orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                // Log the exception here if you have logging configured
                return StatusCode(500, new { success = false, message = "Error processing transaction" });
            }
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
    public class OrderItem
    {
        public string ProductName { get; set; }
        public string Size { get; set; }
        public decimal Price { get; set; }
    }
}
