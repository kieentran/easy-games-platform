using Easy_Games_Software.Models;
using Easy_Games_Software.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Easy_Games_Software.Controllers
{
    /// <summary>
    /// Controller for store browsing and product viewing
    /// </summary>
    public class StoreController : Controller
    {
        private readonly IStockService _stockService;
        private readonly IUserService _userService;
        private readonly ILogger<StoreController> _logger;

        public StoreController(
            IStockService stockService,
            IUserService userService,
            ILogger<StoreController> logger)
        {
            _stockService = stockService;
            _userService = userService;
            _logger = logger;
        }

        // GET: /Store or /
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? category = null, string? search = null, string? sort = null)
        {
            List<StockItem> items;

            // Apply filters
            if (!string.IsNullOrEmpty(search))
            {
                items = await _stockService.SearchStockAsync(search);
                ViewData["SearchTerm"] = search;
            }
            else if (!string.IsNullOrEmpty(category) && Enum.TryParse<StockCategory>(category, out var cat))
            {
                items = await _stockService.GetStockByCategoryAsync(cat);
                ViewData["SelectedCategory"] = category;
            }
            else
            {
                items = await _stockService.GetAllStockAsync();
            }

            // Apply sorting
            items = sort?.ToLower() switch
            {
                "name" => items.OrderBy(i => i.Name).ToList(),
                "price-asc" => items.OrderBy(i => i.Price).ToList(),
                "price-desc" => items.OrderByDescending(i => i.Price).ToList(),
                "rating" => items.OrderByDescending(i => i.Rating).ToList(),
                "newest" => items.OrderByDescending(i => i.DateAdded).ToList(),
                _ => items
            };

            ViewData["CurrentSort"] = sort;
            ViewData["Categories"] = Enum.GetValues<StockCategory>();

            // Get user info for discount display if logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    ViewData["UserTier"] = user.Tier;
                    ViewData["DiscountRate"] = user.GetDiscountRate();
                }
            }

            return View(items);
        }

        // GET: /Store/Details/5
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var item = await _stockService.GetStockByIdAsync(id);

            if (item == null || !item.IsActive)
            {
                return NotFound();
            }

            // Get user info for discount display if logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    ViewData["UserTier"] = user.Tier;
                    ViewData["DiscountRate"] = user.GetDiscountRate();
                    ViewData["DiscountedPrice"] = item.GetDiscountedPrice(user.GetDiscountRate());
                }
            }

            // Get related items (same category)
            var relatedItems = await _stockService.GetStockByCategoryAsync(item.Category);
            ViewData["RelatedItems"] = relatedItems.Where(i => i.Id != item.Id).Take(4).ToList();

            return View(item);
        }

        // GET: /Store/Featured
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Featured()
        {
            var items = await _stockService.GetFeaturedItemsAsync();

            // Get user info for discount display if logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    ViewData["UserTier"] = user.Tier;
                    ViewData["DiscountRate"] = user.GetDiscountRate();
                }
            }

            return View(items);
        }

        // GET: /Store/BestSellers
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> BestSellers()
        {
            var items = await _stockService.GetBestSellersAsync(10);

            // Get user info for discount display if logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    ViewData["UserTier"] = user.Tier;
                    ViewData["DiscountRate"] = user.GetDiscountRate();
                }
            }

            return View(items);
        }

        // GET: /Store/Category/{category}
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Category(string category)
        {
            if (!Enum.TryParse<StockCategory>(category, true, out var cat))
            {
                return NotFound();
            }

            var items = await _stockService.GetStockByCategoryAsync(cat);

            ViewData["CategoryName"] = category;
            ViewData["CategoryDescription"] = GetCategoryDescription(cat);

            // Get user info for discount display if logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    ViewData["UserTier"] = user.Tier;
                    ViewData["DiscountRate"] = user.GetDiscountRate();
                }
            }

            return View(items);
        }

        // AJAX: /Store/QuickView/5
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> QuickView(int id)
        {
            var item = await _stockService.GetStockByIdAsync(id);

            if (item == null || !item.IsActive)
            {
                return NotFound();
            }

            // Get user info for discount display if logged in
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var user = await _userService.GetUserByIdAsync(userId);
                if (user != null)
                {
                    ViewData["UserTier"] = user.Tier;
                    ViewData["DiscountRate"] = user.GetDiscountRate();
                    ViewData["DiscountedPrice"] = item.GetDiscountedPrice(user.GetDiscountRate());
                }
            }

            return PartialView("_QuickView", item);
        }

        // Helper method to get category descriptions
        private string GetCategoryDescription(StockCategory category)
        {
            return category switch
            {
                StockCategory.Game => "Browse our collection of video games across all platforms",
                StockCategory.Action => "Heart-pounding action games for thrill seekers",
                StockCategory.RPG => "Immersive role-playing games with epic storylines",
                StockCategory.Puzzle => "Mind-bending puzzle games to challenge your intellect",
                StockCategory.Sports => "Realistic sports simulations and arcade sports fun",
                StockCategory.Strategy => "Strategic games that test your planning and tactics",
                StockCategory.Book => "Game guides, art books, and gaming literature",
                StockCategory.Toy => "Collectibles, figurines, and gaming merchandise",
                _ => "Browse our diverse collection of products"
            };
        }
    }
}