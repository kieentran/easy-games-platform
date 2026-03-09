using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Easy_Games_Software.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Easy_Games_Software.Controllers
{
    /// <summary>
    /// Controller for shopping cart and checkout operations
    /// </summary>
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStockService _stockService;
        private readonly ISalesService _salesService;
        private readonly IUserService _userService;
        private readonly IRewardService _rewardService;
        private readonly ILogger<CartController> _logger;

        public CartController(
            ApplicationDbContext context,
            IStockService stockService,
            ISalesService salesService,
            IUserService userService,
            IRewardService rewardService,
            ILogger<CartController> logger)
        {
            _context = context;
            _stockService = stockService;
            _salesService = salesService;
            _userService = userService;
            _rewardService = rewardService;
            _logger = logger;
        }

        // GET: /Cart
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var cartItems = await GetUserCartItems(userId);

            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null)
            {
                ViewData["UserTier"] = user.Tier;
                ViewData["DiscountRate"] = user.GetDiscountRate();
                ViewData["UserPoints"] = user.Points;
                ViewData["PointsToNextTier"] = await _rewardService.GetPointsToNextTierAsync(userId);
            }

            return View(cartItems);
        }

        // POST: /Cart/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int itemId, int quantity = 1)
        {
            var userId = GetCurrentUserId();

            // Check if item exists and is available
            var stockItem = await _stockService.GetStockByIdAsync(itemId);
            if (stockItem == null || !stockItem.IsActive)
            {
                TempData["ErrorMessage"] = "Item not found or unavailable.";
                return RedirectToAction("Index", "Store");
            }

            // Check stock availability
            if (!await _stockService.IsStockAvailableAsync(itemId, quantity))
            {
                TempData["ErrorMessage"] = "Insufficient stock available.";
                return RedirectToAction("Details", "Store", new { id = itemId });
            }

            // Check if item already in cart
            var existingCartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.StockItemId == itemId);

            if (existingCartItem != null)
            {
                // Update quantity
                existingCartItem.Quantity += quantity;

                // Check total quantity doesn't exceed stock
                if (existingCartItem.Quantity > stockItem.Quantity)
                {
                    existingCartItem.Quantity = stockItem.Quantity;
                    TempData["WarningMessage"] = $"Quantity adjusted to available stock ({stockItem.Quantity}).";
                }
            }
            else
            {
                // Add new cart item
                var cartItem = new CartItem
                {
                    UserId = userId,
                    StockItemId = itemId,
                    Quantity = Math.Min(quantity, stockItem.Quantity),
                    DateAdded = DateTime.Now
                };

                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{stockItem.Name} added to cart!";
            _logger.LogInformation("User {UserId} added item {ItemId} to cart", userId, itemId);

            // Return to the referring page or store index
            var returnUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Store");
        }

        // POST: /Cart/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int cartItemId, int quantity)
        {
            var userId = GetCurrentUserId();

            var cartItem = await _context.CartItems
                .Include(c => c.StockItem)
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (cartItem == null)
            {
                return NotFound();
            }

            if (quantity <= 0)
            {
                // Remove item if quantity is 0 or less
                _context.CartItems.Remove(cartItem);
                TempData["SuccessMessage"] = "Item removed from cart.";
            }
            else
            {
                // Check stock availability
                if (quantity > cartItem.StockItem.Quantity)
                {
                    TempData["ErrorMessage"] = $"Only {cartItem.StockItem.Quantity} units available.";
                    return RedirectToAction(nameof(Index));
                }

                cartItem.Quantity = quantity;
                TempData["SuccessMessage"] = "Cart updated successfully.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int cartItemId)
        {
            var userId = GetCurrentUserId();

            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (cartItem != null)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Item removed from cart.";
                _logger.LogInformation("User {UserId} removed item from cart", userId);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Clear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            var userId = GetCurrentUserId();

            var cartItems = await _context.CartItems
                .Where(c => c.UserId == userId)
                .ToListAsync();

            _context.CartItems.RemoveRange(cartItems);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cart cleared successfully.";
            _logger.LogInformation("User {UserId} cleared their cart", userId);

            return RedirectToAction(nameof(Index));
        }

        // GET: /Cart/Checkout
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var userId = GetCurrentUserId();
            var cartItems = await GetUserCartItems(userId);

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Index));
            }

            // Validate all items are still available
            foreach (var item in cartItems)
            {
                if (!item.HasSufficientStock())
                {
                    TempData["ErrorMessage"] = $"'{item.StockItem.Name}' has insufficient stock. Please update your cart.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null)
            {
                ViewData["UserTier"] = user.Tier;
                ViewData["DiscountRate"] = user.GetDiscountRate();

                // Calculate totals
                decimal subtotal = cartItems.Sum(c => c.Subtotal);
                decimal discount = subtotal * user.GetDiscountRate();
                decimal total = subtotal - discount;
                int pointsToEarn = _rewardService.CalculatePoints(total, user.Tier);

                ViewData["Subtotal"] = subtotal;
                ViewData["Discount"] = discount;
                ViewData["Total"] = total;
                ViewData["PointsToEarn"] = pointsToEarn;
            }

            return View(cartItems);
        }

        // POST: /Cart/ConfirmPurchase
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPurchase()
        {
            var userId = GetCurrentUserId();
            var cartItems = await GetUserCartItems(userId);

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Index));
            }

            // Process the purchase
            var transaction = await _salesService.ProcessPurchaseAsync(userId, cartItems);

            if (transaction != null)
            {
                TempData["SuccessMessage"] = "Purchase completed successfully!";
                TempData["TransactionId"] = transaction.Id;
                TempData["PointsEarned"] = transaction.PointsEarned;

                _logger.LogInformation("User {UserId} completed purchase. Transaction ID: {TransactionId}",
                    userId, transaction.Id);

                return RedirectToAction(nameof(OrderConfirmation), new { id = transaction.Id });
            }

            TempData["ErrorMessage"] = "Failed to process purchase. Please try again.";
            return RedirectToAction(nameof(Checkout));
        }

        // GET: /Cart/OrderConfirmation/5
        [HttpGet]
        public async Task<IActionResult> OrderConfirmation(int id)
        {
            var userId = GetCurrentUserId();
            var transaction = await _salesService.GetTransactionByIdAsync(id);

            if (transaction == null || transaction.UserId != userId)
            {
                return NotFound();
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null)
            {
                ViewData["UserTier"] = user.Tier;
                ViewData["UserPoints"] = user.Points;
            }

            return View(transaction);
        }

        // AJAX: /Cart/GetCartCount
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            var userId = GetCurrentUserId();
            var count = await _context.CartItems
                .Where(c => c.UserId == userId)
                .SumAsync(c => c.Quantity);

            return Json(new { count });
        }

        // AJAX: /Cart/GetCartSummary
        [HttpGet]
        public async Task<IActionResult> GetCartSummary()
        {
            var userId = GetCurrentUserId();
            var cartItems = await GetUserCartItems(userId);
            var user = await _userService.GetUserByIdAsync(userId);

            decimal subtotal = cartItems.Sum(c => c.Subtotal);
            decimal discount = 0;
            decimal total = subtotal;

            if (user != null)
            {
                discount = subtotal * user.GetDiscountRate();
                total = subtotal - discount;
            }

            return Json(new
            {
                itemCount = cartItems.Count,
                subtotal = subtotal.ToString("C"),
                discount = discount.ToString("C"),
                total = total.ToString("C")
            });
        }

        // Helper method to get current user ID
        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        // Helper method to get user's cart items with related data
        private async Task<List<CartItem>> GetUserCartItems(int userId)
        {
            return await _context.CartItems
                .Include(c => c.StockItem)
                .Include(c => c.User)
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.DateAdded)
                .ToListAsync();
        }
    }
}