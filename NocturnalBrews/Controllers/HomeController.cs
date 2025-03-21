using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NocturnalBrews.Models;
using System.Diagnostics;
using JsonException = Newtonsoft.Json.JsonException;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using Org.BouncyCastle.Asn1.X509;

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
            ViewBag.Addons = _context.Addons.ToList();

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
                .Select(p => new
                {
                    Small = p.Small,
                    Medium = p.Medium,
                    Large = p.Large
                })
                .FirstOrDefault();

            if (product == null)
                return Json(new object[] { });

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
                .Select(p => new
                {
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
            ViewBag.Addons = _context.Addons.ToList();
            return View(products);
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
            catch
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
                if (existingProduct == null) return Json(new { success = false });

                existingProduct.ProductName = product.ProductName;
                existingProduct.Categories = product.Categories;
                existingProduct.Small = product.Small;
                existingProduct.Medium = product.Medium;
                existingProduct.Large = product.Large;

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch
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
                if (product == null) return Json(new { success = false });

                _context.ProductsTbs.Remove(product);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        public IActionResult AddAddon(Addon addon)
        {
            try
            {
                _context.Addons.Add(addon);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        public IActionResult UpdateAddon(Addon addon)
        {
            try
            {
                var existingAddon = _context.Addons.Find(addon.AddonId);
                if (existingAddon == null) return Json(new { success = false });

                existingAddon.AddonName = addon.AddonName;
                existingAddon.AddonPrice = addon.AddonPrice;

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        public IActionResult DeleteAddon(int id)
        {
            try
            {
                var addon = _context.Addons.Find(id);
                if (addon == null) return Json(new { success = false });

                _context.Addons.Remove(addon);
                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch
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


        public IActionResult OrdersDone()
        {
            var orders = _context.OrdersTbs.Where(o => o.Status == "Done").ToList();
            return View(orders);
        }

        [HttpPost]
        public IActionResult GeneratePDF()
        {
            try
            {
                var orders = _context.OrdersTbs
                    .Where(o => o.Status == "Done")
                    .ToList();

                // You'll need to install iTextSharp.LGPLv2.Core NuGet package
                using (MemoryStream ms = new MemoryStream())
                {
                    using (Document document = new Document(PageSize.A4, 25, 25, 30, 30))
                    {
                        PdfWriter writer = PdfWriter.GetInstance(document, ms);
                        document.Open();

                        // Add title
                        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                        var title = new Paragraph("Completed Orders Report", titleFont)
                        {
                            Alignment = Element.ALIGN_CENTER,
                            SpacingAfter = 20f
                        };
                        document.Add(title);

                        // Create table
                        PdfPTable table = new PdfPTable(6) { WidthPercentage = 100 };
                        string[] headers = { "Order ID", "Products", "Total", "Payment Mode", "Order Date", "Status" };

                        // Add headers
                        foreach (string header in headers)
                        {
                            table.AddCell(new PdfPCell(new Phrase(header))
                            {
                                BackgroundColor = new BaseColor(240, 240, 240),
                                Padding = 5
                            });
                        }

                        // Add data
                        foreach (var order in orders)
                        {
                            table.AddCell(order.OrderId.ToString());

                            // Handle products array
                            var products = JsonConvert.DeserializeObject<List<OrderItem>>(order.ProductsArray);
                            var productCell = new PdfPCell();
                            foreach (var item in products)
                            {
                                productCell.AddElement(new Phrase($"{item.ProductName} ({item.Size}) - ₱{item.Price}\n"));
                            }
                            table.AddCell(productCell);

                            table.AddCell($"₱{order.Total}");
                            table.AddCell(order.Mop);
                            table.AddCell(order.OrderDateTime.ToString());
                            table.AddCell(order.Status);
                        }

                        document.Add(table);
                        document.Close();
                    }

                    return File(ms.ToArray(), "application/pdf", "CompletedOrders.pdf");
                }
            }
            catch (Exception ex)
            {
                // Log the error
                return StatusCode(500, "Error generating PDF: " + ex.Message);
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

        [HttpPost]
        public JsonResult UpdateCupInventory(int orderId)
        {
            try
            {
                // Get the order details
                var order = _context.OrdersTbs.FirstOrDefault(o => o.OrderId == orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                // Deserialize the products array
                var orderItems = JsonConvert.DeserializeObject<List<OrderItem>>(order.ProductsArray);

                // Process each ordered item
                foreach (var item in orderItems)
                {
                    // Find the cup inventory record for this size
                    var cupInventory = _context.CupsListTbs.FirstOrDefault(c => c.Size == item.Size);
                    if (cupInventory != null)
                    {
                        // Reduce the cup quantity by 1
                        cupInventory.Quantity -= 1;

                        // Prevent negative inventory
                        if (cupInventory.Quantity < 0)
                        {
                            cupInventory.Quantity = 0;
                        }
                    }
                }

                // Save changes to database
                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Ingredient percentage usage per product and size
        private static Dictionary<string, Dictionary<string, decimal>> IngredientUsage = new Dictionary<string, Dictionary<string, decimal>>
        {
            { "Latte_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 18m } } },
            { "Latte_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m }, { "Milk", 24m } } },
            { "Latte_Large", new Dictionary<string, decimal> { { "Coffee", 7m }, { "Milk", 33m } } },

            { "Dolce Latte_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Cinnamon", 4m }, { "Condensed", 2m }, { "Milk", 18m } } },
            { "Dolce Latte_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m }, { "Cinnamon", 7m }, { "Condensed", 3m }, { "Milk", 24m } } },
            { "Dolce Latte_Large", new Dictionary<string, decimal> { { "Coffee", 7m }, { "Cinnamon", 21m }, { "Condensed", 4m }, { "Milk", 33m } } },

            { "Caramel Macchiato_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 18m }, { "Caramel", 0.3m }, { "Vanilla", 0.15m } } },
            { "Caramel Macchiato_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m }, { "Milk", 24m }, { "Caramel", 0.6m }, { "Vanilla", 0.3m } } },
            { "Caramel Macchiato_Large", new Dictionary<string, decimal> { { "Coffee", 7m }, { "Milk", 33m }, { "Caramel", 0.6m }, { "Vanilla", 0.45m } } },

            { "Choco Hazelnut_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 18m }, { "Hazelnut Syrup", 0.3m }, { "Chocolate Syrup", 0.3m } } },
            { "Choco Hazelnut_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m }, { "Milk", 24m }, { "Hazelnut Syrup", 0.6m }, { "Chocolate Syrup", 0.3m } } },
            { "Choco Hazelnut_Large", new Dictionary<string, decimal> { { "Coffee", 7m }, { "Milk", 33m }, { "Hazelnut Syrup", 0.6m }, { "Chocolate Syrup", 0.3m } } },

            { "White Mocha_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 18m }, { "White Chocolate", 0.3m } } },
            { "White Mocha_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m }, { "Milk", 24m }, { "White Chocolate", 0.5m } } },
            { "White Mocha_Large", new Dictionary<string, decimal> { { "Coffee", 7m }, { "Milk", 33m }, { "White Chocolate", 1.5m } } },

            { "Iced Mocha_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 18m }, { "Chocolate Syrup", 0.3m }, { "Chocolate", 0.5m } } },
            { "Iced Mocha_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m }, { "Milk", 24m }, { "Chocolate Syrup", 0.45m }, { "Chocolate", 0.5m } } },
            { "Iced Mocha_Large", new Dictionary<string, decimal> { { "Coffee", 7m }, { "Milk", 33m }, { "Chocolate Syrup", 0.6m }, { "Chocolate", 0.5m } } },

            { "Americano_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m } } },
            { "Americano_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m } } },
            { "Americano_Large", new Dictionary<string, decimal> { { "Coffee", 7m } } },

            { "Spanish Latte_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Condensed", 2m }, { "Milk", 18m } } },
            { "Spanish Latte_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m }, { "Condensed", 3m }, { "Milk", 24m } } },
            { "Spanish Latte_Large", new Dictionary<string, decimal> { { "Coffee", 7m }, { "Condensed", 4m }, { "Milk", 33m } } },

            { "Matcha Latte_Small", new Dictionary<string, decimal> { { "Matcha", 1.5m }, { "Condensed", 2m }, { "Milk", 18m } } },
            { "Matcha Latte_Medium", new Dictionary<string, decimal> { { "Matcha", 2.5m }, { "Condensed", 2m }, { "Milk", 24m } } },
            { "Matcha Latte_Large", new Dictionary<string, decimal> { { "Matcha", 3.5m }, { "Condensed", 4m }, { "Milk", 33m } } },

            { "Strawberry Matcha_Small", new Dictionary<string, decimal> { { "Matcha", 1.5m }, { "Milk", 18m }, { "Strawberry Jam", 3m }, { "Strawberry Syrup", 0.3m } } },
            { "Strawberry Matcha_Medium", new Dictionary<string, decimal> { { "Matcha", 2.5m }, { "Milk", 24m }, { "Strawberry Syrup", 0.45m }, { "Strawberry Jam", 6m } } },
            { "Strawberry Matcha_Large", new Dictionary<string, decimal> { { "Matcha", 3.5m }, { "Milk", 33m }, { "Strawberry Syrup", 0.6m }, { "Strawberry Jam", 6m } } },

            { "Vanilla Matcha_Small", new Dictionary<string, decimal> { { "Matcha", 1.5m }, { "Milk", 18m }, { "Vanilla Syrup", 0.3m } } },
            { "Vanilla Matcha_Medium", new Dictionary<string, decimal> { { "Matcha", 2.5m }, { "Milk", 24m }, { "Vanilla Syrup", 0.45m } } },
            { "Vanilla Matcha_Large", new Dictionary<string, decimal> { { "Matcha", 3.5m }, { "Milk", 33m }, { "Vanilla Syrup", 0.6m } } },

            { "Blue-Berry Yogurt_Small", new Dictionary<string, decimal> { { "BlueBerry Jam", 3m }, { "Milk", 18m }, { "Yogurt Syrup", 0.3m } } },
            { "Blue-Berry Yogurt_Medium", new Dictionary<string, decimal> { { "BlueBerry Jam", 6m }, { "Milk", 24m }, { "Yogurt Syrup", 0.45m } } },
            { "Blue-Berry Yogurt_Large", new Dictionary<string, decimal> { { "BlueBerry Jam", 6m }, { "Milk", 33m }, { "Yogurt Syrup", 0.6m } } }
        };

        private Dictionary<string, Dictionary<DateTime, decimal>> GetDailyInventory()
        {
            var dailyInventory = new Dictionary<string, Dictionary<DateTime, decimal>>();
            var inventoryItems = _context.InventoryTbs.ToList();

            // Log starting inventory if not already logged for today
            LogDailyInventory(inventoryItems);

            foreach (var item in inventoryItems)
            {
                if (!dailyInventory.ContainsKey(item.Name))
                {
                    dailyInventory[item.Name] = new Dictionary<DateTime, decimal>();
                }
                dailyInventory[item.Name][DateTime.Today] = Math.Round((decimal)(item.Stock - item.Used), 2);
            }
            return dailyInventory;
        }

        private void LogDailyInventory(List<InventoryTb> inventoryItems)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            foreach (var item in inventoryItems)
            {
                // Check if log exists for this item today
                var existingLog = _context.DailyInventoryLogs
                    .FirstOrDefault(d => d.Name == item.Name && d.Date == today);

                if (existingLog == null)
                {
                    // Create new log with current stock level
                    var dailyInventoryLog = new DailyInventoryLog
                    {
                        Name = item.Name,
                        Date = today,
                        StartingStock = (decimal)item.Stock // Explicit cast from decimal? to decimal
                    };
                    _context.DailyInventoryLogs.Add(dailyInventoryLog);
                }

                // Check if usage log exists for this item today
                var existingUsageLog = _context.DailyUsageTbs
                    .FirstOrDefault(d => d.Name == item.Name && d.Date == today);

                if (existingUsageLog == null)
                {
                    // Create new usage log
                    var dailyUsageLog = new DailyUsageTb
                    {
                        Name = item.Name,
                        Date = today,
                        Used = 0
                    };
                    _context.DailyUsageTbs.Add(dailyUsageLog);
                }
            }
            _context.SaveChanges();
        }

        public IActionResult ProcessOrders([FromBody] List<OrderItem> orders)
        {
            try
            {
                foreach (var order in orders)
                {
                    string key = $"{order.ProductName}_{order.Size}";

                    if (IngredientUsage.ContainsKey(key))
                    {
                        foreach (var ingredient in IngredientUsage[key])
                        {
                            string ingredientName = ingredient.Key;
                            decimal percentage = ingredient.Value / 100m;

                            var inventoryItem = _context.InventoryTbs.FirstOrDefault(i => i.Name == ingredientName);
                            if (inventoryItem != null)
                            {
                                decimal amountToUse = Math.Round((decimal)((inventoryItem.Stock - inventoryItem.Used) * percentage), 2);
                                inventoryItem.Used = Math.Round((decimal)(inventoryItem.Used + amountToUse), 2);
                                inventoryItem.Stock = Math.Round((decimal)(inventoryItem.Stock - amountToUse), 2);

                                // Track daily usage
                                var dailyUsage = _context.DailyUsageTbs.FirstOrDefault(d => d.Name == ingredientName && d.Date == DateOnly.FromDateTime(DateTime.Today));
                                if (dailyUsage == null)
                                {
                                    dailyUsage = new DailyUsageTb
                                    {
                                        Name = ingredientName,
                                        Date = DateOnly.FromDateTime(DateTime.Today),
                                        Used = 0
                                    };
                                    _context.DailyUsageTbs.Add(dailyUsage);
                                }
                                dailyUsage.Used = Math.Round((decimal)(dailyUsage.Used + amountToUse), 2);
                            }
                        }
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult Inventory()
        {
            var inventoryItems = _context.InventoryTbs.ToList();
            // Ensure daily logs are created when viewing inventory
            GetDailyInventory();
            return View(inventoryItems);
        }

    }


    public class OrderItem
    {
        public string ProductName { get; set; }
        public string Size { get; set; }
        public decimal Price { get; set; }
    }
}
