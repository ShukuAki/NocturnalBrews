using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NocturnalBrews.Models;
using System.Diagnostics;
using JsonException = Newtonsoft.Json.JsonException;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

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

        public IActionResult Inventory()
        {
            var inventoryItems = _context.InventoryTbs.ToList();
            var cups = _context.CupsListTbs.ToList();

            var viewModel = new InventoryViewModel
            {
                InventoryItems = inventoryItems
            };

            ViewBag.Cups = cups;
            return View(viewModel);
        }

        // Inventory Item Methods
        [HttpGet]
        public IActionResult GetInventoryItem(int id)
        {
            var item = _context.InventoryTbs.Find(id);
            return Json(item);
        }

        [HttpPost]
        public IActionResult AddInventoryItem(InventoryTb item)
        {
            _context.InventoryTbs.Add(item);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult UpdateInventoryItem(InventoryTb item)
        {
            var existingItem = _context.InventoryTbs.Find(item.InventoryId);
            if (existingItem == null) return NotFound();

            existingItem.Ingredient = item.Ingredient;
            existingItem.Quantity = item.Quantity;
            existingItem.PricePer = item.PricePer;
            existingItem.PriceWholeSale = item.PriceWholeSale;

            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult DeleteInventoryItem(int id)
        {
            var item = _context.InventoryTbs.Find(id);
            if (item == null) return NotFound();

            _context.InventoryTbs.Remove(item);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        // Cup Methods
        [HttpGet]
        public IActionResult GetCup(int id)
        {
            var cup = _context.CupsListTbs.Find(id);
            return Json(cup);
        }

        [HttpPost]
        public IActionResult AddCup(CupsListTb cup)
        {
            _context.CupsListTbs.Add(cup);
            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult UpdateCup(CupsListTb cup)
        {
            var existingCup = _context.CupsListTbs.Find(cup.CupId);
            if (existingCup == null) return NotFound();

            existingCup.Size = cup.Size;
            existingCup.Quantity = cup.Quantity;
            existingCup.PricePerCup = cup.PricePerCup;
            existingCup.PriceWholeSale = cup.PriceWholeSale;

            _context.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult DeleteCup(int id)
        {
            var cup = _context.CupsListTbs.Find(id);
            if (cup == null) return NotFound();

            _context.CupsListTbs.Remove(cup);
            _context.SaveChanges();
            return Json(new { success = true });
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
    }
    public class OrderItem
    {
        public string ProductName { get; set; }
        public string Size { get; set; }
        public decimal Price { get; set; }
    }
}
