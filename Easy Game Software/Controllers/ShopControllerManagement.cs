using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Easy_Games_Software.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

// Claude Prompt 011 start
namespace Easy_Games_Software.Controllers
{
    [Authorize(Roles = "Owner")]
    public class ShopManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IShopService _shopService;
        private readonly IUserService _userService;
        private readonly ILogger<ShopManagementController> _logger;

        public ShopManagementController(
            ApplicationDbContext context,
            IShopService shopService,
            IUserService userService,
            ILogger<ShopManagementController> logger)
        {
            _context = context;
            _shopService = shopService;
            _userService = userService;
            _logger = logger;
        }

        // GET: /ShopManagement
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var shops = await _shopService.GetAllShopsAsync(activeOnly: false);
            return View(shops);
        }

        // GET: /ShopManagement/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var shop = await _shopService.GetShopByIdAsync(id);
            if (shop == null)
            {
                return NotFound();
            }

            var stats = await _shopService.GetShopStatisticsAsync(id);
            ViewData["Statistics"] = stats;

            return View(shop);
        }

        // GET: /ShopManagement/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Get users who can be shop proprietors (exclude Owners)
            var availableProprietors = await _context.Users
                .Where(u => u.IsActive && u.Role != UserRole.Owner)
                .OrderBy(u => u.Username)
                .ToListAsync();

            ViewBag.Proprietors = new SelectList(
                availableProprietors,
                "Id",
                "Username"
            );

            return View();
        }

        // POST: /ShopManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Shop model)
        {
            // DEBUG: Log what we received
            _logger.LogInformation("=== CREATE SHOP DEBUG ===");
            _logger.LogInformation($"ShopName: {model.ShopName}");
            _logger.LogInformation($"Location: {model.Location}");
            _logger.LogInformation($"PhoneNumber: {model.PhoneNumber}");
            _logger.LogInformation($"ShopProprietorId: {model.ShopProprietorId}");
            _logger.LogInformation($"ModelState.IsValid: {ModelState.IsValid}");

            // DEBUG: Log validation errors
            if (!ModelState.IsValid)
            {
                _logger.LogError("=== MODEL STATE ERRORS ===");
                foreach (var error in ModelState)
                {
                    foreach (var e in error.Value.Errors)
                    {
                        _logger.LogError($"Field: {error.Key}, Error: {e.ErrorMessage}");
                    }
                }

                await LoadProprietorsDropdown();
                return View(model);
            }

            try
            {
                // Update the proprietor's role
                var proprietor = await _userService.GetUserByIdAsync(model.ShopProprietorId);
                if (proprietor != null && proprietor.Role != UserRole.Owner)
                {
                    proprietor.Role = UserRole.ShopProprietor;
                    await _userService.UpdateUserAsync(proprietor);
                }

                var success = await _shopService.CreateShopAsync(model);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Shop '{model.ShopName}' created successfully!";
                    _logger.LogInformation("Shop created successfully");
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogError("CreateShopAsync returned false");
                TempData["ErrorMessage"] = "Failed to create shop.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating shop");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            await LoadProprietorsDropdown();
            return View(model);
        }

        // GET: /ShopManagement/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var shop = await _shopService.GetShopByIdAsync(id);
            if (shop == null)
            {
                return NotFound();
            }

            await LoadProprietorsDropdown();
            return View(shop);
        }

        // POST: /ShopManagement/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Shop model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                await LoadProprietorsDropdown();
                return View(model);
            }

            var existingShop = await _shopService.GetShopByIdAsync(id);
            if (existingShop == null)
            {
                return NotFound();
            }

            // Update proprietor role if changed
            if (existingShop.ShopProprietorId != model.ShopProprietorId)
            {
                var newProprietor = await _userService.GetUserByIdAsync(model.ShopProprietorId);
                if (newProprietor != null && newProprietor.Role != UserRole.Owner)
                {
                    newProprietor.Role = UserRole.ShopProprietor;
                    await _userService.UpdateUserAsync(newProprietor);
                }
            }

            // Update shop details
            existingShop.ShopName = model.ShopName;
            existingShop.Location = model.Location;
            existingShop.PhoneNumber = model.PhoneNumber;
            existingShop.ShopProprietorId = model.ShopProprietorId;
            existingShop.IsActive = model.IsActive;
            existingShop.Notes = model.Notes;

            var success = await _shopService.UpdateShopAsync(existingShop);

            if (success)
            {
                TempData["SuccessMessage"] = "Shop updated successfully.";
                _logger.LogInformation("Shop {ShopId} updated by {CurrentUser}",
                    id, User.Identity?.Name);
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Failed to update shop.";
            await LoadProprietorsDropdown();
            return View(model);
        }

        // POST: /ShopManagement/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var shop = await _shopService.GetShopByIdAsync(id);
                if (shop == null)
                {
                    TempData["ErrorMessage"] = "Shop not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Hard delete - remove shop and related data
                _context.Shops.Remove(shop);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Shop '{shop.ShopName}' deleted successfully.";
                _logger.LogInformation("Shop {ShopId} deleted permanently by {CurrentUser}",
                    id, User.Identity?.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shop {ShopId}", id);
                TempData["ErrorMessage"] = "Failed to delete shop. It may have associated transactions.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Helper method to load proprietors dropdown
        private async Task LoadProprietorsDropdown()
        {
            var availableProprietors = await _context.Users
                .Where(u => u.IsActive && u.Role != UserRole.Owner)
                .OrderBy(u => u.Username)
                .ToListAsync();

            ViewBag.Proprietors = new SelectList(
                availableProprietors,
                "Id",
                "Username"
            );
        }
    }
}
// Claude Prompt 011 end