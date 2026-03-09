using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Easy_Games_Software.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Easy_Games_Software.Controllers
{
    /// <summary>
    /// Controller for owner/admin operations
    /// </summary>
    [Authorize(Roles = "Owner")]
    public class OwnerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IStockService _stockService;
        private readonly ISalesService _salesService;
        private readonly IRewardService _rewardService;
        private readonly IFileService _fileService;
        private readonly ILogger<OwnerController> _logger;

        public OwnerController(
            ApplicationDbContext context,
            IUserService userService,
            IStockService stockService,
            ISalesService salesService,
            IRewardService rewardService,
            IFileService fileService,
            ILogger<OwnerController> logger)
        {
            _context = context;
            _userService = userService;
            _stockService = stockService;
            _salesService = salesService;
            _rewardService = rewardService;
            _fileService = fileService;
            _logger = logger;
        }

        // Claude Prompt 006 start
        // GET: /Owner
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Dashboard with key metrics
            var viewModel = new OwnerDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(u => u.IsActive),
                TotalCustomers = await _context.Users.CountAsync(u => u.IsActive && u.Role == UserRole.User),
                TotalOwners = await _context.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Owner),
                TotalStock = await _context.StockItems.CountAsync(s => s.IsActive),
                TotalTransactions = await _context.Transactions.CountAsync(t => t.Status == TransactionStatus.Completed),
                TotalRevenue = await _salesService.GetTotalRevenueAsync(),
                RecentTransactions = await _salesService.GetRecentTransactionsAsync(5),
                BestSellers = await _stockService.GetBestSellersAsync(5),
                TopCustomers = await _salesService.GetTopCustomersAsync(5),
                SalesStatistics = await _salesService.GetSalesStatisticsAsync(),
                StockByCategory = await _stockService.GetStockStatisticsAsync(),
                TierDistribution = await _rewardService.GetTierDistributionAsync()
            };

            // Calculate month-over-month growth
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);
            var lastMonthRevenue = await _salesService.GetRevenueByDateRangeAsync(startOfLastMonth, startOfMonth);
            var thisMonthRevenue = await _salesService.GetRevenueByDateRangeAsync(startOfMonth, DateTime.Now);

            if (lastMonthRevenue > 0)
            {
                viewModel.GrowthPercentage = ((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100;
            }

            return View(viewModel);
        }
        // Claude Prompt 006 end

        // GET: /Owner/Users
        [HttpGet]
        public async Task<IActionResult> Users(string? search = null, UserRole? role = null)
        {
            var query = _context.Users.Where(u => u.IsActive).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(search) ||
                                       (u.Email != null && u.Email.ToLower().Contains(search)) ||
                                       (u.FullName != null && u.FullName.ToLower().Contains(search)));
                ViewData["SearchTerm"] = search;
            }

            if (role.HasValue)
            {
                query = query.Where(u => u.Role == role.Value);
                ViewData["SelectedRole"] = role.Value;
            }

            var users = await query.OrderBy(u => u.Username).ToListAsync();
            return View(users);
        }

        // GET: /Owner/UserDetails/5
        [HttpGet]
        public async Task<IActionResult> UserDetails(int id)
        {
            var user = await _context.Users
                .Include(u => u.Transactions)
                    .ThenInclude(t => t.StockItem)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            ViewData["PointsToGold"] = await _rewardService.GetPointsToNextTierAsync(id);
            return View(user);
        }

        // POST: /Owner/PromoteUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PromoteUser(int id)
        {
            var success = await _userService.PromoteToOwnerAsync(id);

            if (success)
            {
                TempData["SuccessMessage"] = "User promoted to Owner successfully.";
                _logger.LogInformation("User {UserId} promoted to Owner by {CurrentUser}",
                    id, User.Identity?.Name);
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to promote user.";
            }

            return RedirectToAction(nameof(Users));
        }

        // POST: /Owner/DeleteUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            // Prevent self-deletion
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            var success = await _userService.DeleteUserAsync(id);

            if (success)
            {
                TempData["SuccessMessage"] = "User deleted successfully.";
                _logger.LogInformation("User {UserId} deleted by {CurrentUser}",
                    id, User.Identity?.Name);
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete user.";
            }

            return RedirectToAction(nameof(Users));
        }

        // Claude Prompt 007 start
        // GET: /Owner/Stock
        [HttpGet]
        public async Task<IActionResult> Stock(string? search = null, StockCategory? category = null)
        {
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
                items = await _stockService.GetAllStockAsync(activeOnly: false);
            }

            ViewData["Categories"] = Enum.GetValues<StockCategory>();
            return View(items);
        }

        // GET: /Owner/CreateStock
        [HttpGet]
        public IActionResult CreateStock()
        {
            ViewData["Categories"] = Enum.GetValues<StockCategory>();
            return View();
        }

        // POST: /Owner/CreateStock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStock(StockItem model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["Categories"] = Enum.GetValues<StockCategory>();
                return View(model);
            }

            var success = await _stockService.AddStockItemAsync(model);

            if (success)
            {
                TempData["SuccessMessage"] = "Stock item created successfully.";
                _logger.LogInformation("Stock item {ItemName} created by {CurrentUser}",
                    model.Name, User.Identity?.Name);
                return RedirectToAction(nameof(Stock));
            }

            TempData["ErrorMessage"] = "Failed to create stock item.";
            ViewData["Categories"] = Enum.GetValues<StockCategory>();
            return View(model);
        }

        // GET: /Owner/EditStock/5
        [HttpGet]
        public async Task<IActionResult> EditStock(int id)
        {
            var item = await _stockService.GetStockByIdAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            ViewBag.Categories = new SelectList(
                Enum.GetValues<StockCategory>().Cast<StockCategory>(),
                item.Category
            );

            return View(item);
        }

        // POST: /Owner/EditStock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStock(int id, StockItem model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                ViewData["Categories"] = Enum.GetValues<StockCategory>();
                return View(model);
            }

            try
            {
                var existingItem = await _stockService.GetStockByIdAsync(id);

                if (existingItem == null)
                {
                    return NotFound();
                }

                // ✅ Update all editable fields
                existingItem.Name = model.Name;
                existingItem.Description = model.Description;
                existingItem.Category = model.Category;
                existingItem.Price = model.Price;
                existingItem.Quantity = model.Quantity;
                existingItem.Rating = model.Rating;
                existingItem.IsFeatured = model.IsFeatured;
                existingItem.IsActive = model.IsActive;

                var success = await _stockService.UpdateStockItemAsync(existingItem);

                if (success)
                {
                    TempData["SuccessMessage"] = "Stock item updated successfully.";
                    _logger.LogInformation("Stock item {ItemId} updated by {CurrentUser}",
                        id, User.Identity?.Name);
                    return RedirectToAction(nameof(Stock));
                }

                TempData["ErrorMessage"] = "Failed to update stock item.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock item {ItemId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while updating the stock item.";
            }

            ViewData["Categories"] = Enum.GetValues<StockCategory>();
            return View(model);
        }

        // POST: /Owner/DeleteStock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStock(int id)
        {
            var success = await _stockService.DeleteStockItemAsync(id);

            if (success)
            {
                TempData["SuccessMessage"] = "Stock item deleted successfully.";
                _logger.LogInformation("Stock item {ItemId} deleted by {CurrentUser}",
                    id, User.Identity?.Name);
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete stock item. It may have already been removed or an error occurred.";
                _logger.LogWarning("Failed to delete stock item {ItemId} attempted by {CurrentUser}",
                    id, User.Identity?.Name);
            }

            return RedirectToAction(nameof(Stock));
        }
        // Claude Prompt 007 end

        // GET: /Owner/Transactions
        [HttpGet]
        public async Task<IActionResult> Transactions(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Transactions
                .Include(t => t.User)
                .Include(t => t.StockItem)
                .Where(t => t.Status == TransactionStatus.Completed)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate >= startDate.Value);
                ViewData["StartDate"] = startDate.Value.ToString("yyyy-MM-dd");
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate <= endDate.Value);
                ViewData["EndDate"] = endDate.Value.ToString("yyyy-MM-dd");
            }

            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .Take(100) // Limit to recent 100 for performance
                .ToListAsync();

            // Calculate summary
            ViewData["TotalRevenue"] = transactions.Sum(t => t.TotalAmount);
            ViewData["TotalDiscounts"] = transactions.Sum(t => t.DiscountAmount);
            ViewData["TotalPoints"] = transactions.Sum(t => t.PointsEarned);

            return View(transactions);
        }

        // Claude Prompt 008 start
        // GET: /Owner/Reports
        [HttpGet]
        public IActionResult Reports()
        {
            return View();
        }

        // POST: /Owner/GenerateReport
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReport(DateTime startDate, DateTime endDate, string reportType)
        {
            byte[] reportData;
            string fileName;
            string contentType;

            switch (reportType)
            {
                case "sales":
                    reportData = await _fileService.GenerateSalesReportAsync(startDate, endDate);
                    fileName = $"SalesReport_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.txt";
                    contentType = "text/plain";
                    break;

                case "users":
                    var usersFile = Path.GetTempFileName();
                    await _fileService.ExportUsersToCSVAsync(usersFile);
                    reportData = await System.IO.File.ReadAllBytesAsync(usersFile);
                    System.IO.File.Delete(usersFile);
                    fileName = $"Users_{DateTime.Now:yyyyMMdd}.csv";
                    contentType = "text/csv";
                    break;

                case "stock":
                    var stockFile = Path.GetTempFileName();
                    await _fileService.ExportStockToCSVAsync(stockFile);
                    reportData = await System.IO.File.ReadAllBytesAsync(stockFile);
                    System.IO.File.Delete(stockFile);
                    fileName = $"Stock_{DateTime.Now:yyyyMMdd}.csv";
                    contentType = "text/csv";
                    break;

                case "transactions":
                    var transFile = Path.GetTempFileName();
                    await _fileService.ExportTransactionsToCSVAsync(transFile);
                    reportData = await System.IO.File.ReadAllBytesAsync(transFile);
                    System.IO.File.Delete(transFile);
                    fileName = $"Transactions_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
                    contentType = "text/csv";
                    break;

                default:
                    TempData["ErrorMessage"] = "Invalid report type.";
                    return RedirectToAction(nameof(Reports));
            }

            _logger.LogInformation("Report {ReportType} generated by {CurrentUser}",
                reportType, User.Identity?.Name);

            return File(reportData, contentType, fileName);
        }

        // GET: /Owner/ImportStock
        [HttpGet]
        public IActionResult ImportStock()
        {
            return View();
        }

        // POST: /Owner/ImportStock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStock(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a file to import.";
                return View();
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Please upload a CSV file.";
                return View();
            }

            var tempFile = Path.GetTempFileName();

            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var success = await _fileService.ImportStockFromCSVAsync(tempFile);

                if (success)
                {
                    TempData["SuccessMessage"] = "Stock imported successfully.";
                    _logger.LogInformation("Stock imported from CSV by {CurrentUser}", User.Identity?.Name);
                    return RedirectToAction(nameof(Stock));
                }

                TempData["ErrorMessage"] = "Failed to import stock.";
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                {
                    System.IO.File.Delete(tempFile);
                }
            }

            return View();
        }
        // Claude Prompt 008 end

        #region Email Management

        // Claude Prompt 010 start
        // GET: /Owner/SendEmail - UPDATED FOR 4 TIERS
        [HttpGet]
        public async Task<IActionResult> SendEmail()
        {
            var model = new SendEmailViewModel
            {
                TotalCustomers = await _context.Users.CountAsync(u => u.IsActive && u.Role == UserRole.User),
                BronzeCustomers = await _context.Users.CountAsync(u => u.IsActive && u.Role == UserRole.User && u.Tier == UserTier.Bronze),
                SilverCustomers = await _context.Users.CountAsync(u => u.IsActive && u.Role == UserRole.User && u.Tier == UserTier.Silver),
                GoldCustomers = await _context.Users.CountAsync(u => u.IsActive && u.Role == UserRole.User && u.Tier == UserTier.Gold),
                PlatinumCustomers = await _context.Users.CountAsync(u => u.IsActive && u.Role == UserRole.User && u.Tier == UserTier.Platinum)
            };

            return View(model);
        }

        // POST: /Owner/SendEmailToTier
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmailToTier(string tier, string subject, string message)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Subject and message are required.";
                return RedirectToAction(nameof(SendEmail));
            }

            try
            {
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                bool success = false;

                if (tier == "All")
                {
                    success = await emailService.SendEmailToAllCustomersAsync(subject, message);
                }
                else if (Enum.TryParse<UserTier>(tier, out var userTier))
                {
                    success = await emailService.SendEmailToTierAsync(userTier, subject, message);
                }

                if (success)
                {
                    TempData["SuccessMessage"] = $"Email sent successfully to {tier} tier customers!";
                    _logger.LogInformation("Promotional email sent to {Tier} by {User}", tier, User.Identity?.Name);
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to send email. Please check logs.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to tier {Tier}", tier);
                TempData["ErrorMessage"] = "An error occurred while sending the email.";
            }

            return RedirectToAction(nameof(SendEmail));
        }

        // POST: /Owner/SendEmailToUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmailToUser(int userId, string subject, string message)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Subject and message are required.";
                return RedirectToAction(nameof(Users));
            }

            try
            {
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                var success = await emailService.SendEmailToUserAsync(userId, subject, message);

                if (success)
                {
                    TempData["SuccessMessage"] = "Email sent successfully to user!";
                    _logger.LogInformation("Email sent to user {UserId} by {User}", userId, User.Identity?.Name);
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to send email. User may not have an email address.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to user {UserId}", userId);
                TempData["ErrorMessage"] = "An error occurred while sending the email.";
            }

            return RedirectToAction(nameof(Users));
        }
        // Claude Prompt 010 end

        #endregion
    }

    // Claude Prompt 006 start
    // View model for owner dashboard
    public class OwnerDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalOwners { get; set; }
        public int TotalStock { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal GrowthPercentage { get; set; }
        public List<Transaction> RecentTransactions { get; set; } = new();
        public List<StockItem> BestSellers { get; set; } = new();
        public List<(User user, decimal totalSpent)> TopCustomers { get; set; } = new();
        public Dictionary<string, decimal> SalesStatistics { get; set; } = new();
        public Dictionary<StockCategory, int> StockByCategory { get; set; } = new();
        public Dictionary<UserTier, int> TierDistribution { get; set; } = new();
    }
    // Claude Prompt 006 end

    // Claude Prompt 010 start
    // View model for sending emails - UPDATED FOR 4 TIERS
    public class SendEmailViewModel
    {
        public int TotalCustomers { get; set; }
        public int BronzeCustomers { get; set; }
        public int SilverCustomers { get; set; }
        public int GoldCustomers { get; set; }
        public int PlatinumCustomers { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SelectedTier { get; set; } = "All";
    }
    // Claude Prompt 010 end
}