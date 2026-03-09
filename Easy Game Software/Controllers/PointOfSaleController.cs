using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Easy_Games_Software.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

// Claude Prompt 012 start
namespace Easy_Games_Software.Controllers
{

    [Authorize(Roles = "ShopProprietor,Owner")]
    public class PointOfSaleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IShopService _shopService;
        private readonly IUserService _userService;
        private readonly IRewardService _rewardService;
        private readonly ILogger<PointOfSaleController> _logger;

        public PointOfSaleController(
            ApplicationDbContext context,
            IShopService shopService,
            IUserService userService,
            IRewardService rewardService,
            ILogger<PointOfSaleController> logger)
        {
            _context = context;
            _shopService = shopService;
            _userService = userService;
            _rewardService = rewardService;
            _logger = logger;
        }

        // GET: /PointOfSale
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var shop = await GetCurrentUserShop();
            if (shop == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to any shop.";
                return RedirectToAction("Index", "Store");
            }

            ViewData["ShopName"] = shop.ShopName;
            ViewData["ShopId"] = shop.Id;

            return View();
        }

        // AJAX: /PointOfSale/SearchCustomer
        [HttpPost]
        public async Task<IActionResult> SearchCustomer(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return Json(new { success = false, message = "Phone number is required." });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.IsActive);

            if (user != null)
            {
                return Json(new
                {
                    success = true,
                    customer = new
                    {
                        id = user.Id,
                        username = user.Username,
                        fullName = user.FullName,
                        tier = user.Tier.ToString(),
                        points = user.Points,
                        discountRate = user.GetDiscountRate(),
                        phoneNumber = user.PhoneNumber
                    }
                });
            }

            return Json(new { success = false, message = "Customer not found." });
        }

        // GET: /PointOfSale/QuickSignup
        [HttpGet]
        public IActionResult QuickSignup()
        {
            return View();
        }

        // POST: /PointOfSale/QuickSignup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickSignup(QuickSignupViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if phone number already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);

            if (existingUser != null)
            {
                ModelState.AddModelError("PhoneNumber", "This phone number is already registered.");
                return View(model);
            }

            var user = new User
            {
                Username = model.PhoneNumber, // Use phone as username
                Password = _userService.HashPassword(model.PhoneNumber.Substring(0, 6)), // Simple default password
                PhoneNumber = model.PhoneNumber,
                FullName = model.FullName,
                Email = model.Email,
                Role = UserRole.User,
                Tier = UserTier.Silver,
                Points = 0,
                TotalSpent = 0,
                RegistrationDate = DateTime.Now,
                IsActive = true
            };

            var success = await _userService.RegisterUserAsync(user);

            if (success)
            {
                TempData["SuccessMessage"] = $"Customer {model.FullName} registered successfully!";
                _logger.LogInformation("Quick signup for customer {Phone} at POS", model.PhoneNumber);
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, "Failed to register customer.");
            return View(model);
        }

        // AJAX: /PointOfSale/GetShopStock
        [HttpGet]
        public async Task<IActionResult> GetShopStock(string? search = null)
        {
            var shop = await GetCurrentUserShop();
            if (shop == null)
            {
                return Json(new { success = false, message = "Shop not found." });
            }

            var query = _context.ShopStocks
                .Include(ss => ss.StockItem)
                .Where(ss => ss.ShopId == shop.Id && ss.StockItem.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(ss => ss.StockItem.Name.ToLower().Contains(search));
            }

            var items = await query
                .Select(ss => new
                {
                    id = ss.StockItem.Id,
                    name = ss.StockItem.Name,
                    price = ss.StockItem.Price,
                    quantityInShop = ss.QuantityInShop,
                    isLowStock = ss.IsLowStock()
                })
                .ToListAsync();

            return Json(new { success = true, items });
        }

        // POST: /PointOfSale/CompleteSale
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteSale([FromBody] POSSaleViewModel model)
        {
            var shop = await GetCurrentUserShop();
            if (shop == null)
            {
                return Json(new { success = false, message = "Shop not found." });
            }

            if (model.Items == null || !model.Items.Any())
            {
                return Json(new { success = false, message = "No items in sale." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                User? customer = null;
                decimal discountRate = 0;

                // Get customer if provided
                if (model.CustomerId.HasValue && model.CustomerId.Value > 0)
                {
                    customer = await _userService.GetUserByIdAsync(model.CustomerId.Value);
                    if (customer != null)
                    {
                        discountRate = customer.GetDiscountRate();
                    }
                }

                var transactions = new List<Transaction>();
                decimal totalAmount = 0;
                int totalPoints = 0;

                foreach (var item in model.Items)
                {
                    // Get shop stock
                    var shopStock = await _context.ShopStocks
                        .Include(ss => ss.StockItem)
                        .FirstOrDefaultAsync(ss => ss.ShopId == shop.Id && ss.StockItemId == item.StockItemId);

                    if (shopStock == null)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = $"Item {item.StockItemId} not found in shop inventory." });
                    }

                    // Allow sale even at zero stock per requirements
                    // Deduct from shop stock (can go negative)
                    shopStock.QuantityInShop -= item.Quantity;
                    _context.ShopStocks.Update(shopStock);

                    // Create transaction
                    var trans = new Transaction
                    {
                        UserId = customer?.Id ?? 1, // Default to admin if no customer
                        StockItemId = shopStock.StockItemId,
                        ShopId = shop.Id, // IMPORTANT: Record which shop made the sale
                        Quantity = item.Quantity,
                        UnitPrice = shopStock.Price,
                        TransactionDate = DateTime.Now,
                        Status = TransactionStatus.Completed
                    };

                    trans.CalculateTotals(discountRate);

                    if (customer != null)
                    {
                        trans.CalculatePoints(customer.GetPointsMultiplier());
                        totalPoints += trans.PointsEarned;
                    }

                    trans.GenerateReference();
                    _context.Transactions.Add(trans);
                    transactions.Add(trans);

                    totalAmount += trans.TotalAmount;
                }

                // Update customer points and tier if customer identified
                if (customer != null)
                {
                    customer.Points += totalPoints;
                    customer.TotalSpent += totalAmount;
                    customer.UpdateTier();
                    _context.Users.Update(customer);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "POS Sale completed at Shop {ShopId}. Total: {Total}, Customer: {CustomerId}",
                    shop.Id, totalAmount, customer?.Id);

                return Json(new
                {
                    success = true,
                    message = "Sale completed successfully!",
                    totalAmount,
                    pointsEarned = totalPoints,
                    transactionReference = transactions.First().TransactionReference
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error completing POS sale at Shop {ShopId}", shop.Id);
                return Json(new { success = false, message = "Failed to complete sale. Please try again." });
            }
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

    // View Models
    public class QuickSignupViewModel
    {
        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Email (Optional)")]
        public string? Email { get; set; }
    }

    public class POSSaleViewModel
    {
        public int? CustomerId { get; set; }
        public List<POSSaleItemViewModel> Items { get; set; } = new();
    }

    public class POSSaleItemViewModel
    {
        public int StockItemId { get; set; }
        public int Quantity { get; set; }
    }
}
// Claude Prompt 012 end