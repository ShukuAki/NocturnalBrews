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
using OfficeOpenXml;
using OfficeOpenXml.Style;
using ClosedXML.Excel;

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
            // Initialize daily inventory records when application starts
            InitializeDailyInventory();

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
            { "Latte_Small", new Dictionary<string, decimal> { { "Coffee", 1.5m }, { "Milk", 12m }, { "12oz Cups", 1m } } },
            { "Latte_Medium", new Dictionary<string, decimal> { { "Coffee", 2.5m }, { "Milk", 14m }, { "16oz Cups", 1m } } },
            { "Latte_Large", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 16m }, { "22oz Cups", 1m } } },

            { "Dolce Latte_Small", new Dictionary<string, decimal> { { "Coffee", 1.5m }, { "Cinnamon", 4m }, { "Condensed", 1.5m }, { "Milk", 12m }, { "12oz Cups", 1m } } },
            { "Dolce Latte_Medium", new Dictionary<string, decimal> { { "Coffee", 2.5m }, { "Cinnamon", 7m }, { "Condensed", 3m }, { "Milk", 14m }, { "16oz Cups", 1m } } },
            { "Dolce Latte_Large", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Cinnamon", 14m }, { "Condensed", 5m }, { "Milk", 16m }, { "22oz Cups", 1m } } },

            { "Caramel Macchiato_Small", new Dictionary<string, decimal> { { "Coffee", 1.5m }, { "Milk", 12m }, { "Caramel", 0.3m }, { "Vanilla", 0.15m }, { "12oz Cups", 1m } } },
            { "Caramel Macchiato_Medium", new Dictionary<string, decimal> { { "Coffee", 2.5m }, { "Milk", 14m }, { "Caramel", 0.6m }, { "Vanilla", 0.3m }, { "16oz Cups", 1m } } },
            { "Caramel Macchiato_Large", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 16m }, { "Caramel", 0.85m }, { "Vanilla", 0.60m }, { "22oz Cups", 1m } } },

            { "Choco Hazelnut_Small", new Dictionary<string, decimal> { { "Coffee", 1.5m }, { "Milk", 12m }, { "Hazelnut Syrup", 0.3m }, { "12oz Cups", 1m } } },
            { "Choco Hazelnut_Medium", new Dictionary<string, decimal> { { "Coffee", 2.5m }, { "Milk", 14m }, { "Hazelnut Syrup", 0.6m }, { "16oz Cups", 1m } } },
            { "Choco Hazelnut_Large", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 16m }, { "Hazelnut Syrup", 0.85m }, { "22oz Cups", 1m } } },

            { "White Mocha_Small", new Dictionary<string, decimal> { { "Coffee", 1.5m }, { "Milk", 12m }, { "White Chocolate Bar", 0.3m }, { "White Chocolate Syrup", 0.3m }, { "12oz Cups", 1m } } },
            { "White Mocha_Medium", new Dictionary<string, decimal> { { "Coffee", 2.5m }, { "Milk", 14m }, { "White Chocolate Bar", 0.5m }, { "White Chocolate Syrup", 0.6m }, { "16oz Cups", 1m } } },
            { "White Mocha_Large", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 16m }, { "White Chocolate Bar", 1.5m }, { "White Chocolate Syrup", 0.85m }, { "22oz Cups", 1m } } },

            { "Iced Mocha_Small", new Dictionary<string, decimal> { { "Coffee", 1.5m }, { "Milk", 12m }, { "Chocolate Syrup", 0.3m }, { "Chocolate Bar", 0.5m }, { "12oz Cups", 1m } } },
            { "Iced Mocha_Medium", new Dictionary<string, decimal> { { "Coffee", 2.5m }, { "Milk", 14m }, { "Chocolate Syrup", 0.6m }, { "Chocolate Bar", 1m }, { "16oz Cups", 1m } } },
            { "Iced Mocha_Large", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Milk", 18m }, { "Chocolate Syrup", 0.85m }, { "Chocolate Bar", 1.5m }, { "22oz Cups", 1m } } },

            { "Americano_Small", new Dictionary<string, decimal> { { "Coffee", 1.5m }, { "Water", 18m }, { "12oz Cups", 1m } } },
            { "Americano_Medium", new Dictionary<string, decimal> { { "Coffee", 2.5m }, { "Water", 18m }, { "16oz Cups", 1m } } },
            { "Americano_Large", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Water", 18m }, { "22oz Cups", 1m } } },

            { "Spanish Latte_Small", new Dictionary<string, decimal> { { "Coffee", 3.5m }, { "Condensed", 2m }, { "Milk", 18m }, { "12oz Cups", 1m } } },
            { "Spanish Latte_Medium", new Dictionary<string, decimal> { { "Coffee", 5.5m }, { "Condensed", 3m }, { "Milk", 24m }, { "16oz Cups", 1m } } },
            { "Spanish Latte_Large", new Dictionary<string, decimal> { { "Coffee", 7m }, { "Condensed", 4m }, { "Milk", 33m }, { "22oz Cups", 1m } } },

            { "Matcha Latte_Small", new Dictionary<string, decimal> { { "Matcha", 1.5m }, { "Condensed", 2m }, { "Milk", 18m }, { "12oz Cups", 1m } } },
            { "Matcha Latte_Medium", new Dictionary<string, decimal> { { "Matcha", 2.5m }, { "Condensed", 2m }, { "Milk", 24m }, { "16oz Cups", 1m } } },
            { "Matcha Latte_Large", new Dictionary<string, decimal> { { "Matcha", 3.5m }, { "Condensed", 4m }, { "Milk", 33m }, { "22oz Cups", 1m } } },

            { "Strawberry Matcha_Small", new Dictionary<string, decimal> { { "Matcha", 1.5m }, { "Milk", 18m }, { "Strawberry Jam", 3m }, { "Strawberry Syrup", 0.3m }, { "12oz Cups", 1m } } },
            { "Strawberry Matcha_Medium", new Dictionary<string, decimal> { { "Matcha", 2.5m }, { "Milk", 24m }, { "Strawberry Syrup", 0.45m }, { "Strawberry Jam", 6m }, { "16oz Cups", 1m } } },
            { "Strawberry Matcha_Large", new Dictionary<string, decimal> { { "Matcha", 3.5m }, { "Milk", 33m }, { "Strawberry Syrup", 0.6m }, { "Strawberry Jam", 6m }, { "22oz Cups", 1m } } },

            { "Vanilla Matcha_Small", new Dictionary<string, decimal> { { "Matcha", 1.5m }, { "Milk", 18m }, { "Vanilla Syrup", 0.3m }, { "12oz Cups", 1m } } },
            { "Vanilla Matcha_Medium", new Dictionary<string, decimal> { { "Matcha", 2.5m }, { "Milk", 24m }, { "Vanilla Syrup", 0.45m }, { "16oz Cups", 1m } } },
            { "Vanilla Matcha_Large", new Dictionary<string, decimal> { { "Matcha", 3.5m }, { "Milk", 33m }, { "Vanilla Syrup", 0.6m }, { "22oz Cups", 1m } } },

            { "Blue-Berry Yogurt_Small", new Dictionary<string, decimal> { { "BlueBerry Jam", 3m }, { "Milk", 12m }, { "Yogurt Syrup", 0.30m }, { "12oz Cups", 1m } } },
            { "Blue-Berry Yogurt_Medium", new Dictionary<string, decimal> { { "BlueBerry Jam", 6m }, { "Milk", 14m }, { "Yogurt Syrup", 0.60m }, { "16oz Cups", 1m } } },
            { "Blue-Berry Yogurt_Large", new Dictionary<string, decimal> { { "BlueBerry Jam", 10m }, { "Milk", 16m }, { "Yogurt Syrup", 0.85m }, { "22oz Cups", 1m } } },

            { "Strawberry Yogurt_Small", new Dictionary<string, decimal> { { "Strawberry Jam", 3m }, { "Milk", 12m }, { "Yogurt Syrup", 0.30m }, { "12oz Cups", 1m } } },
            { "Strawberry Yogurt_Medium", new Dictionary<string, decimal> { { "Strawberry Jam", 6m }, { "Milk", 14m }, { "Yogurt Syrup", 0.60m }, { "16oz Cups", 1m } } },
            { "Strawberry Yogurt_Large", new Dictionary<string, decimal> { { "Strawberry Jam", 10m }, { "Milk", 16m }, { "Yogurt Syrup", 0.85m }, { "22oz Cups", 1m } } },

            { "Mango Yogurt_Small", new Dictionary<string, decimal> { { "Mango Jam", 3m }, { "Milk", 12m }, { "Yogurt Syrup", 0.30m }, { "12oz Cups", 1m } } },
            { "Mango Yogurt_Medium", new Dictionary<string, decimal> { { "Mango Jam", 6m }, { "Milk", 14m }, { "Yogurt Syrup", 0.60m }, { "16oz Cups", 1m } } },
            { "Mango Yogurt_Large", new Dictionary<string, decimal> { { "Mango Jam", 10m }, { "Milk", 16m }, { "Yogurt Syrup", 0.85m }, { "22oz Cups", 1m } } }
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
                dailyInventory[item.Name][DateTime.Today] = Math.Round((decimal)(item.Stock - item.Used + item.InventoryIncoming), 2);
            }
            return dailyInventory;
        }

        private void LogDailyInventory(List<InventoryTb> inventoryItems)
        {
            var today = DateTime.Today;

            // Get all unique item names from the inventory
            var allItemNames = _context.InventoryTbs
                .Select(i => i.Name)
                .Distinct()
                .ToList();

            foreach (var itemName in allItemNames)
            {
                // Check if today's record exists
                var todayRecord = _context.InventoryTbs
                    .FirstOrDefault(i => i.Name == itemName && i.DateToday.Date == today);

                if (todayRecord == null)
                {
                    // Get yesterday's record
                    var yesterdayRecord = _context.InventoryTbs
                        .Where(i => i.Name == itemName && i.DateToday.Date < today)
                        .OrderByDescending(i => i.DateToday)
                        .FirstOrDefault();

                    // Get the measurement from the most recent record
                    var measurement = yesterdayRecord?.Measurement ?? "N/A";

                    // Calculate ending stock from yesterday
                    decimal endingStock = 0;
                    if (yesterdayRecord != null)
                    {
                        // Keep yesterday's record unchanged and use its final values for today's starting point
                        endingStock = yesterdayRecord.Stock - yesterdayRecord.Used + yesterdayRecord.InventoryIncoming;
                    }

                    // Create new record for today
                    var newRecord = new InventoryTb
                    {
                        Name = itemName,
                        DateToday = today,
                        Stock = endingStock,  // Start with yesterday's ending balance
                        Used = 0,  // Reset usage for the new day
                        InventoryIncoming = 0,  // Reset incoming for the new day
                        Measurement = measurement,
                    };

                    _context.InventoryTbs.Add(newRecord);  // Add as new record
                }
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error in LogDailyInventory: {ex.Message}");
            }
        }

        public IActionResult ProcessOrders([FromBody] List<OrderItem> orders)
        {
            try
            {
                var today = DateTime.Today;

                foreach (var order in orders)
                {
                    string key = $"{order.ProductName}_{order.Size}";

                    if (IngredientUsage.ContainsKey(key))
                    {
                        foreach (var ingredient in IngredientUsage[key])
                        {
                            string ingredientName = ingredient.Key;
                            decimal amountToUse;

                            // Special handling for cups
                            if (ingredientName.Contains("Cups"))
                            {
                                amountToUse = ingredient.Value; // Use the direct value (1) for cups
                            }
                            else
                            {
                                decimal percentage = ingredient.Value / 100m;
                                amountToUse = Math.Round((decimal)(percentage), 2);
                            }

                            var inventoryItem = _context.InventoryTbs
                                .FirstOrDefault(i => i.Name == ingredientName && i.DateToday.Date == today);

                            if (inventoryItem != null)
                            {
                                inventoryItem.Used = Math.Round((decimal)(inventoryItem.Used + amountToUse), 2);
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
            var today = DateTime.Today;
            var inventoryItems = _context.InventoryTbs
                .Where(i => i.DateToday.Date == today)
                .ToList();
                
            // Ensure daily records are created
            GetDailyInventory();
            return View(inventoryItems);
        }

        public IActionResult GetInventoryHistory(string itemName, DateTime startDate, DateTime endDate)
        {
            try
            {
                var history = _context.InventoryTbs
                    .Where(inv => inv.Name == itemName
                             && inv.DateToday.Date >= startDate.Date
                             && inv.DateToday.Date <= endDate.Date)
                    .OrderBy(inv => inv.DateToday)
                    .Select(inv => new
                    {
                        name = inv.Name,
                        date = inv.DateToday.ToString("yyyy-MM-dd"),
                        startingStock = Math.Round(inv.Stock, 2),
                        used = Math.Round(inv.Used, 2),
                        incoming = Math.Round(inv.InventoryIncoming, 2),
                        endStock = Math.Round(inv.Stock - inv.Used + inv.InventoryIncoming, 2)
                    }).ToList();

                return Json(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult GetDailyHistory(int days = 7)  // Default to 7 days
        {
            try
            {
                var endDate = DateTime.Today;
                var startDate = endDate.AddDays(-days + 1);

                var history = _context.InventoryTbs
                    .Where(inv => inv.DateToday.Date >= startDate && inv.DateToday.Date <= endDate)
                    .OrderByDescending(inv => inv.DateToday)
                    .ThenBy(inv => inv.Name)
                    .Select(inv => new
                    {
                        name = inv.Name,
                        date = inv.DateToday.ToString("yyyy-MM-dd"),
                        startingStock = Math.Round(inv.Stock, 2),
                        used = Math.Round(inv.Used, 2),
                        incoming = Math.Round(inv.InventoryIncoming, 2),
                        endStock = Math.Round(inv.Stock - inv.Used + inv.InventoryIncoming, 2)
                    }).ToList();

                return Json(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult UpdateStock([FromBody] UpdateStockModel model)
        {
            try
            {
                var today = DateTime.Today;
                var item = _context.InventoryTbs
                    .FirstOrDefault(i => i.Name == model.Name && i.DateToday.Date == today);
                    
                if (item != null)
                {
                    item.InventoryIncoming += model.Stock - item.Stock;
                    item.Stock = model.Stock;
                    _context.SaveChanges();
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = "Item not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class UpdateStockModel
        {
            public string Name { get; set; }
            public decimal Stock { get; set; }
        }

        public void InitializeDailyInventory()
        {
            var today = DateTime.Today;

            // Get all unique item names from the inventory
            var allItemNames = _context.InventoryTbs
                .Select(i => i.Name)
                .Distinct()
                .ToList();

            foreach (var itemName in allItemNames)
            {
                // Check if today's record exists
                var todayRecord = _context.InventoryTbs
                    .FirstOrDefault(i => i.Name == itemName && i.DateToday.Date == today);

                if (todayRecord == null)
                {
                    // Get yesterday's record
                    var yesterdayRecord = _context.InventoryTbs
                        .Where(i => i.Name == itemName && i.DateToday.Date < today)
                        .OrderByDescending(i => i.DateToday)
                        .FirstOrDefault();

                    // Calculate ending stock from yesterday
                    decimal endingStock = 0;
                    if (yesterdayRecord != null)
                    {
                        endingStock = Math.Round(yesterdayRecord.Stock - yesterdayRecord.Used + yesterdayRecord.InventoryIncoming, 2);
                    }

                    // Create new record for today
                    var newRecord = new InventoryTb
                    {
                        Name = itemName,
                        DateToday = today,
                        Stock = endingStock,
                        Used = 0,
                        InventoryIncoming = 0,
                    };

                    _context.InventoryTbs.Add(newRecord);
                }
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error initializing daily inventory: {ex.Message}");
            }
        }

        [HttpPost]
        public IActionResult AddInventoryStock([FromBody] AddStockModel model)
        {
            try
            {
                var today = DateTime.Today;
                var item = _context.InventoryTbs
                    .FirstOrDefault(i => i.Name == model.Name && i.DateToday.Date == today);
                    
                if (item != null)
                {
                    item.InventoryIncoming += model.AdditionalStock;
                    _context.SaveChanges();
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = "Item not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class AddStockModel
        {
            public string Name { get; set; }
            public decimal AdditionalStock { get; set; }
        }

        [HttpGet]
        public IActionResult GetInventoryHistoryByDate(string date)
        {
            try
            {
                // Parse the date string to DateTime
                if (!DateTime.TryParse(date, out DateTime selectedDate))
                {
                    return Json(new { success = false, message = "Invalid date format" });
                }

                // Get inventory data for the selected date
                var inventoryData = _context.InventoryTbs
                    .Where(inv => inv.DateToday.Date == selectedDate.Date)
                    .Select(inv => new
                    {
                        name = inv.Name,
                        startingStock = Math.Round(inv.Stock, 2),
                        used = Math.Round(inv.Used, 2),
                        incoming = Math.Round(inv.InventoryIncoming, 2),
                        endStock = Math.Round(inv.Stock - inv.Used + inv.InventoryIncoming, 2),
                        measurement = inv.Measurement
                    })
                    .ToList();

                if (!inventoryData.Any())
                {
                    return Json(new { success = false, message = "No data found for selected date." });
                }

                return Json(new { success = true, data = inventoryData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class AddNewInventoryItemModel
        {
            public string Name { get; set; }
            public decimal Stock { get; set; }
            public string Measurement { get; set; }
        }

        [HttpPost]
        public IActionResult AddNewInventoryItem([FromBody] AddNewInventoryItemModel model)
        {
            try
            {
                // Check if item already exists for today
                var today = DateTime.Today;
                var existingItem = _context.InventoryTbs
                    .FirstOrDefault(i => i.Name == model.Name && i.DateToday.Date == today);

                if (existingItem != null)
                {
                    return Json(new { success = false, message = "Item already exists for today" });
                }

                // Create new inventory item
                var newItem = new InventoryTb
                {
                    Name = model.Name,
                    Stock = model.Stock,
                    Used = 0,
                    InventoryIncoming = 0,
                    DateToday = today,
                    Measurement = model.Measurement
                };

                _context.InventoryTbs.Add(newItem);
                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class UpdateInventoryItemModel
        {
            public string OriginalName { get; set; }
            public string NewName { get; set; }
            public decimal Stock { get; set; }
            public decimal Used { get; set; }
            public decimal Incoming { get; set; }
        }

        [HttpPost]
        public IActionResult UpdateInventoryItem([FromBody] UpdateInventoryItemModel model)
        {
            try
            {
                var today = DateTime.Today;
                var item = _context.InventoryTbs
                    .FirstOrDefault(i => i.Name == model.OriginalName && i.DateToday.Date == today);
                    
                if (item != null)
                {
                    item.Name = model.NewName;
                    item.Stock = model.Stock;
                    item.Used = model.Used;
                    item.InventoryIncoming = model.Incoming;
                    
                    _context.SaveChanges();
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = "Item not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult ExportInventorySingleDate(string date)
        {
            try
            {
                if (!DateTime.TryParse(date, out DateTime selectedDate))
                {
                    return BadRequest("Invalid date format");
                }

                // Define the ordered list of items
                var orderedItemNames = new[]
                {
                    "Coffee",
                    "Matcha",
                    "Milk",
                    "Condensed",
                    "Caramel",
                    "Hazelnut Syrup",
                    "Vanilla",
                    "White Chocolate Syrup",
                    "Chocolate Syrup",
                    "Yogurt Syrup",
                    "Strawberry Syrup",
                    "Chocolate Sauce",
                    "Caramel Sauce",
                    "White Chocolate Bar",
                    "Chocolate Bar",
                    "Cinnamon",
                    "Honey",
                    "Whipped Cream",
                    "Strawberry Jam",
                    "Mango Jam",
                    "BlueBerry Jam",
                    "12oz Cups",
                    "16oz Cups",
                    "22oz Cups",
                    "Ice",
                    "Water"
                };

                // Get inventory data
                var inventoryData = _context.InventoryTbs
                    .Where(inv => inv.DateToday.Date == selectedDate.Date)
                    .ToDictionary(inv => inv.Name);

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add($"Inventory {selectedDate:yyyy-MM-dd}");

                    // Add headers
                    var headers = new[] { 
                        "Item Name", 
                        "Inventory", 
                        "Outbound Delivery", 
                        "Damage",
                        "Total",
                        "Inbound Delivery",
                        "Returns",
                        "Stocks on Hand"
                    };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = headers[i];
                        worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    }

                    // Add data in specified order
                    int row = 2;
                    foreach (var itemName in orderedItemNames)
                    {
                        if (inventoryData.TryGetValue(itemName, out var item))
                        {
                            // Column positions (using 1-based indexing)
                            int inventoryCol = 2;
                            int outboundCol = 3;
                            int damageCol = 4;
                            int totalCol = 5;
                            int inboundCol = 6;
                            int returnsCol = 7;
                            int stocksOnHandCol = 8;

                            // Set values
                            worksheet.Cell(row, 1).Value = item.Name;
                            worksheet.Cell(row, inventoryCol).Value = item.Stock;
                            worksheet.Cell(row, outboundCol).Value = item.Used;
                            worksheet.Cell(row, damageCol).Value = ""; // Empty for damage
                            worksheet.Cell(row, inboundCol).Value = item.InventoryIncoming;
                            worksheet.Cell(row, returnsCol).Value = ""; // Empty for returns
                            
                            // Set formulas
                            var totalCell = worksheet.Cell(row, totalCol);
                            totalCell.FormulaA1 = $"=B{row}-C{row}"; // Total = Inventory - Outbound
                            
                            var stocksOnHandCell = worksheet.Cell(row, stocksOnHandCol);
                            stocksOnHandCell.FormulaA1 = $"=E{row}+F{row}"; // Stocks on Hand = Total + Inbound

                            row++;
                        }
                    }

                    // Style the table
                    var tableRange = worksheet.Range(1, 1, row - 1, 8);
                    tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                    tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    
                    // Format number columns to show 2 decimal places
                    worksheet.Range(2, 2, row - 1, 8).Style.NumberFormat.NumberFormatId = 2;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        stream.Position = 0;

                        return File(
                            stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Inventory_{selectedDate:yyyy-MM-dd}.xlsx"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        public IActionResult ExportInventoryDateRange(string startDate, string endDate)
        {
            try
            {
                if (!DateTime.TryParse(startDate, out DateTime start) || 
                    !DateTime.TryParse(endDate, out DateTime end))
                {
                    return BadRequest("Invalid date format");
                }

                // Define the ordered list of items
                var orderedItemNames = new[]
                {
                    "Coffee",
                    "Matcha",
                    "Milk",
                    "Condensed",
                    "Caramel",
                    "Hazelnut Syrup",
                    "Vanilla",
                    "White Chocolate Syrup",
                    "Chocolate Syrup",
                    "Yogurt Syrup",
                    "Strawberry Syrup",
                    "Chocolate Sauce",
                    "Caramel Sauce",
                    "White Chocolate Bar",
                    "Chocolate Bar",
                    "Cinnamon",
                    "Honey",
                    "Whipped Cream",
                    "Strawberry Jam",
                    "Mango Jam",
                    "BlueBerry Jam",
                    "12oz Cups",
                    "16oz Cups",
                    "22oz Cups",
                    "Ice",
                    "Water"
                };

                using (var workbook = new XLWorkbook())
                {
                    for (DateTime date = start.Date; date <= end.Date; date = date.AddDays(1))
                    {
                        var inventoryData = _context.InventoryTbs
                            .Where(inv => inv.DateToday.Date == date.Date)
                            .ToDictionary(inv => inv.Name);

                        if (inventoryData.Any())
                        {
                            var worksheet = workbook.Worksheets.Add($"{date:yyyy-MM-dd}");

                            // Add headers
                            var headers = new[] { 
                                "Item Name", 
                                "Inventory", 
                                "Outbound Delivery", 
                                "Damage",
                                "Total",
                                "Inbound Delivery",
                                "Returns",
                                "Stocks on Hand"
                            };
                            for (int i = 0; i < headers.Length; i++)
                            {
                                worksheet.Cell(1, i + 1).Value = headers[i];
                                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                            }

                            // Add data in specified order
                            int row = 2;
                            foreach (var itemName in orderedItemNames)
                            {
                                if (inventoryData.TryGetValue(itemName, out var item))
                                {
                                    // Column positions (using 1-based indexing)
                                    int inventoryCol = 2;
                                    int outboundCol = 3;
                                    int damageCol = 4;
                                    int totalCol = 5;
                                    int inboundCol = 6;
                                    int returnsCol = 7;
                                    int stocksOnHandCol = 8;

                                    // Set values
                                    worksheet.Cell(row, 1).Value = item.Name;
                                    worksheet.Cell(row, inventoryCol).Value = item.Stock;
                                    worksheet.Cell(row, outboundCol).Value = item.Used;
                                    worksheet.Cell(row, damageCol).Value = ""; // Empty for damage
                                    worksheet.Cell(row, inboundCol).Value = item.InventoryIncoming;
                                    worksheet.Cell(row, returnsCol).Value = ""; // Empty for returns
                                    
                                    // Set formulas
                                    var totalCell = worksheet.Cell(row, totalCol);
                                    totalCell.FormulaA1 = $"=B{row}-C{row}"; // Total = Inventory - Outbound
                                    
                                    var stocksOnHandCell = worksheet.Cell(row, stocksOnHandCol);
                                    stocksOnHandCell.FormulaA1 = $"=E{row}+F{row}"; // Stocks on Hand = Total + Inbound

                                    row++;
                                }
                            }

                            // Style the table
                            var tableRange = worksheet.Range(1, 1, row - 1, 8);
                            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                            
                            // Format number columns to show 2 decimal places
                            worksheet.Range(2, 2, row - 1, 8).Style.NumberFormat.NumberFormatId = 2;
                        }
                    }

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        stream.Position = 0;

                        return File(
                            stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Inventory_{start:yyyy-MM-dd}_to_{end:yyyy-MM-dd}.xlsx"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
