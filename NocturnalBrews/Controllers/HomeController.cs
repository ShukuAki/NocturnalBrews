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
            var products = _context.ProductsTbs.ToList();

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


        [HttpGet]
        public JsonResult GetSizes(string productName)
        {
            var product = _context.ProductsTbs
                .Where(p => p.ProductName == productName)
                .Select(p => new { 
                    Small = p.Small,
                    Medium = p.Medium, 
                    Large = p.Large
                })
                .FirstOrDefault();

            if (product == null)
                return Json(new object[] {});

            var sizes = new List<object>();
            if (product.Small.HasValue) sizes.Add(new { size = "Small", price = product.Small.Value });
            if (product.Medium.HasValue) sizes.Add(new { size = "Medium", price = product.Medium.Value });
            if (product.Large.HasValue) sizes.Add(new { size = "Large", price = product.Large.Value });

            return Json(sizes);
        }

        [HttpGet]
        public async Task<IActionResult> GetPrice(string productName, string size)
        {
            var product = await _context.ProductsTbs
                .Where(p => p.ProductName == productName)
                .Select(p => new { 
                    Small = p.Small,
                    Medium = p.Medium,
                    Large = p.Large
                })
                .FirstOrDefaultAsync();

            if (product == null)
                return NotFound();

            int? price = size.ToLower() switch
            {
                "small" => product.Small,
                "medium" => product.Medium, 
                "large" => product.Large,
                _ => null
            };

            if (!price.HasValue)
                return NotFound();

            return Json(new { price = price.Value });
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
                    OrderDateTime = DateTime.Now,
                    Status = "Pending"
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

            public IActionResult Maintenance()
        {
            var products = _context.ProductsTbs.ToList();
            return View(products);  // Pass the products to the view
        }

        [HttpPost]
        public IActionResult AddProduct(ProductsTb product)
        {
            try
            {
                _context.ProductsTbs.Add(product);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception)
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        public IActionResult UpdateProduct(ProductsTb product)
        {
            try
            {
                var existingProduct = _context.ProductsTbs.Find(product.ProductId);
                if (existingProduct == null)
                    return Json(new { success = false });

                existingProduct.ProductName = product.ProductName;
                existingProduct.Small = product.Small;
                existingProduct.Medium = product.Medium;
                existingProduct.Large = product.Large;

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception)
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        public IActionResult DeleteProduct(int id)
        {
            try
            {
                var product = _context.ProductsTbs.Find(id);
                if (product == null)
                    return Json(new { success = false });

                _context.ProductsTbs.Remove(product);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception)
            {
                return Json(new { success = false });
            }
        }


        //pending orders
        public IActionResult PendingOrder()
        {
            var pendingOrders = _context.OrdersTbs
                .Where(o => o.Status == "Pending")
                .OrderByDescending(o => o.OrderDateTime)
                .ToList();

            return View(pendingOrders);
        }

        // POST: Home/UpdateOrderStatus
        [HttpPost]
        public IActionResult UpdateOrderStatus(int orderId, string status)
        {
            try
            {
                var order = _context.OrdersTbs.Find(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                order.Status = status;
                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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
