using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Easy_Games_Software.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

// Claude Prompt 013 start
namespace Easy_Games_Software.Controllers
{
    [Authorize(Roles = "ShopProprietor,Owner")]
    public class ShopInventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IShopService _shopService;
        private readonly IStockService _stockService;
        private readonly ILogger<ShopInventoryController> _logger;

        public ShopInventoryController(
            ApplicationDbContext context,
            IShopService shopService,
            IStockService stockService,
            ILogger<ShopInventoryController> logger)
        {
            _context = context;
            _shopService = shopService;
            _stockService = stockService;
            _logger = logger;
        }

        // GET: /ShopInventory/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var shop = await GetCurrentUserShop();
            if (shop == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any shop.";
                return RedirectToAction("Index", "Store");
            }

            var stats = await _shopService.GetShopStatisticsAsync(shop.Id);
            ViewData["Statistics"] = stats;

            return View(shop);
        }

        // GET: /ShopInventory/AvailableStock
        [HttpGet]
        public async Task<IActionResult> AvailableStock(string? search = null, StockCategory? category = null)
        {
            var shop = await GetCurrentUserShop();
            if (shop == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any shop.";
                return RedirectToAction("Index", "Store");
            }

            List<StockItem> items;

            if (!string.IsNullOrEmpty(search))
            {
                items = await _stockService.SearchStockAsync(search);
                ViewData["SearchTerm"] = search;
            }
            else if (category.HasValue)
            {
                items = await _stockService.GetStockByCategoryAsync(category.Value);
                ViewData["SelectedCategory"] = category.Value;
            }
            else
            {
                items = await _stockService.GetAllStockAsync();
            }

            ViewData["Categories"] = Enum.GetValues<StockCategory>();
            ViewData["ShopId"] = shop.Id;
            ViewData["ShopName"] = shop.ShopName;

            return View(items);
        }

        // GET: /ShopInventory/Transfer?stockItemId=5
        [HttpGet]
        public async Task<IActionResult> Transfer(int stockItemId)
        {
            var shop = await GetCurrentUserShop();
            if (shop == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any shop.";
                return RedirectToAction("Index", "Store");
            }

            var stockItem = await _stockService.GetStockByIdAsync(stockItemId);
            if (stockItem == null)
            {
                return NotFound();
            }

            // Check if item already exists in shop
            var existingShopStock = await _context.ShopStocks
                .FirstOrDefaultAsync(ss => ss.ShopId == shop.Id && ss.StockItemId == stockItemId);

            var model = new ShopStockTransferViewModel
            {
                ShopId = shop.Id,
                ShopName = shop.ShopName,
                StockItemId = stockItem.Id,
                StockItemName = stockItem.Name,
                AvailableQuantity = stockItem.Quantity,
                CurrentShopQuantity = existingShopStock?.QuantityInShop ?? 0,
                Price = stockItem.Price,
                QuantityToTransfer = 0
            };

            return View(model);
        }

        // POST: /ShopInventory/Transfer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer(ShopStockTransferViewModel model)
        {
            var shop = await GetCurrentUserShop();
            if (shop == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any shop.";
                return RedirectToAction("Index", "Store");
            }

            if (model.QuantityToTransfer <= 0)
            {
                ModelState.AddModelError("QuantityToTransfer", "Quantity must be greater than 0.");
            }

            // Validate owner has sufficient stock
            var stockItem = await _stockService.GetStockByIdAsync(model.StockItemId);
            if (stockItem == null)
            {
                TempData["ErrorMessage"] = "Stock item not found.";
                return RedirectToAction(nameof(AvailableStock));
            }

            if (stockItem.Quantity < model.QuantityToTransfer)
            {
                ModelState.AddModelError("QuantityToTransfer",
                    $"Only {stockItem.Quantity} units available in owner's inventory.");
            }

            if (!ModelState.IsValid)
            {
                model.ShopName = shop.ShopName;
                model.StockItemName = stockItem.Name;
                model.AvailableQuantity = stockItem.Quantity;
                model.Price = stockItem.Price;
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Deduct from owner's inventory
                stockItem.Quantity -= model.QuantityToTransfer;
                _context.StockItems.Update(stockItem);

                // Add to shop inventory
                var shopStock = await _context.ShopStocks
                    .FirstOrDefaultAsync(ss => ss.ShopId == shop.Id && ss.StockItemId == stockItem.Id);

                if (shopStock != null)
                {
                    // Update existing
                    shopStock.AddStock(model.QuantityToTransfer);
                    _context.ShopStocks.Update(shopStock);
                }
                else
                {
                    // Create new
                    shopStock = new ShopStock
                    {
                        ShopId = shop.Id,
                        StockItemId = stockItem.Id,
                        QuantityInShop = model.QuantityToTransfer,
                        LastRestocked = DateTime.Now,
                        DateAdded = DateTime.Now,
                        Notes = "Initial transfer"
                    };
                    _context.ShopStocks.Add(shopStock);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    $"Successfully transferred {model.QuantityToTransfer} units of '{stockItem.Name}' to {shop.ShopName}.";

                _logger.LogInformation(
                    "Transferred {Quantity} units of {ItemName} to Shop {ShopId} by {User}",
                    model.QuantityToTransfer, stockItem.Name, shop.Id, User.Identity?.Name);

                return RedirectToAction(nameof(ViewShopStock));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error transferring stock to shop {ShopId}", shop.Id);
                TempData["ErrorMessage"] = "Failed to transfer stock. Please try again.";
                return RedirectToAction(nameof(AvailableStock));
            }
        }

        // GET: /ShopInventory/ViewShopStock
        [HttpGet]
        public async Task<IActionResult> ViewShopStock()
        {
            var shop = await GetCurrentUserShop();
            if (shop == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any shop.";
                return RedirectToAction("Index", "Store");
            }

            var shopStocks = await _context.ShopStocks
                .Include(ss => ss.StockItem)
                .Where(ss => ss.ShopId == shop.Id)
                .OrderBy(ss => ss.StockItem.Name)
                .ToListAsync();

            ViewData["ShopName"] = shop.ShopName;
            return View(shopStocks);
        }

        // Helper method to get current user's shop
        private async Task<Shop?> GetCurrentUserShop()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // If owner, get first active shop for testing
            if (User.IsInRole("Owner"))
            {
                return await _context.Shops
                    .Include(s => s.ShopStocks)
                        .ThenInclude(ss => ss.StockItem)
                    .FirstOrDefaultAsync(s => s.IsActive);
            }

            // For shop proprietor, get their assigned shop
            return await _shopService.GetShopByProprietorIdAsync(userId);
        }
    }

    // View Model for stock transfer
    public class ShopStockTransferViewModel
    {
        public int ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public int StockItemId { get; set; }
        public string StockItemName { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; }
        public int CurrentShopQuantity { get; set; }
        public decimal Price { get; set; }
        public int QuantityToTransfer { get; set; }
        public string? Notes { get; set; }
    }
}
// Claude Prompt 013 end