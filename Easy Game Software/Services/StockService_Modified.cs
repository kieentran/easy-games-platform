using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Microsoft.EntityFrameworkCore;

// Claude Prompt 027 start
namespace Easy_Games_Software.Services
{
    /// <summary>
    /// Interface for stock-related operations
    /// </summary>
    public interface IStockService
    {
        Task<List<StockItem>> GetAllStockAsync(bool activeOnly = true);
        Task<List<StockItem>> GetStockByCategoryAsync(StockCategory category);
        Task<StockItem?> GetStockByIdAsync(int id);
        Task<bool> AddStockItemAsync(StockItem item);
        Task<bool> UpdateStockItemAsync(StockItem item);
        Task<bool> DeleteStockItemAsync(int id);
        Task<bool> UpdateStockQuantityAsync(int id, int quantity);
        Task<List<StockItem>> SearchStockAsync(string searchTerm);
        Task<List<StockItem>> GetFeaturedItemsAsync();
        Task<List<StockItem>> GetBestSellersAsync(int count = 5);
        Task<Dictionary<StockCategory, int>> GetStockStatisticsAsync();
        Task<bool> IsStockAvailableAsync(int id, int requestedQuantity);
    }

    /// <summary>
    /// Stock service implementation with email notification support
    /// </summary>
    public class StockService : IStockService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StockService> _logger;
        private readonly IEmailService _emailService;

        public StockService(
            ApplicationDbContext context,
            ILogger<StockService> logger,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        public async Task<List<StockItem>> GetAllStockAsync(bool activeOnly = true)
        {
            var query = _context.StockItems.AsQueryable();

            if (activeOnly)
                query = query.Where(s => s.IsActive);

            return await query.OrderBy(s => s.Name).ToListAsync();
        }

        public async Task<List<StockItem>> GetStockByCategoryAsync(StockCategory category)
        {
            return await _context.StockItems
                .Where(s => s.Category == category && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<StockItem?> GetStockByIdAsync(int id)
        {
            return await _context.StockItems
                .Include(s => s.Transactions)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        /// <summary>
        /// Add new stock item and automatically notify all customers via email
        /// </summary>
        public async Task<bool> AddStockItemAsync(StockItem item)
        {
            try
            {
                item.DateAdded = DateTime.Now;
                item.IsActive = true;

                _context.StockItems.Add(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Stock item {ItemName} added successfully", item.Name);

                // Send email notification to all customers
                await NotifyCustomersAboutNewStockAsync(item);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding stock item {ItemName}", item.Name);
                return false;
            }
        }

        /// <summary>
        /// Notify all active customers about new stock via email
        /// </summary>
        private async Task NotifyCustomersAboutNewStockAsync(StockItem stockItem)
        {
            try
            {
                // Get all active customers (not owners or shop proprietors)
                var customers = await _context.Users
                    .Where(u => u.IsActive &&
                                u.Role == UserRole.User &&
                                !string.IsNullOrEmpty(u.Email))
                    .ToListAsync();

                if (!customers.Any())
                {
                    _logger.LogWarning("No customers found to notify about new stock: {ItemName}", stockItem.Name);
                    return;
                }

                // Send email notification
                var emailSent = await _emailService.SendNewStockNotificationAsync(stockItem, customers);

                if (emailSent)
                {
                    _logger.LogInformation(
                        "Email notification sent to {Count} customers about new stock: {ItemName}",
                        customers.Count,
                        stockItem.Name);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to send email notification about new stock: {ItemName}",
                        stockItem.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying customers about new stock: {ItemName}", stockItem.Name);
                // Don't throw - email notification failure shouldn't prevent stock addition
            }
        }

        public async Task<bool> UpdateStockItemAsync(StockItem item)
        {
            try
            {
                item.LastUpdated = DateTime.Now;
                _context.StockItems.Update(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Stock item {ItemId} updated successfully", item.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock item {ItemId}", item.Id);
                return false;
            }
        }

        public async Task<bool> DeleteStockItemAsync(int id)
        {
            try
            {
                var item = await GetStockByIdAsync(id);
                if (item == null) return false;

                // Delete related transactions first
                var relatedTransactions = _context.Transactions
                    .Where(t => t.StockItemId == id);

                if (relatedTransactions.Any())
                {
                    _context.Transactions.RemoveRange(relatedTransactions);
                }

                // Hard delete stock item
                _context.StockItems.Remove(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Stock item {ItemId} and its related transactions deleted successfully", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting stock item {ItemId}", id);
                return false;
            }
        }

        public async Task<bool> UpdateStockQuantityAsync(int id, int quantity)
        {
            try
            {
                var item = await GetStockByIdAsync(id);
                if (item == null) return false;

                item.Quantity = quantity;
                item.LastUpdated = DateTime.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Stock quantity for item {ItemId} updated to {Quantity}", id, quantity);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock quantity for item {ItemId}", id);
                return false;
            }
        }

        public async Task<List<StockItem>> SearchStockAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<StockItem>();

            searchTerm = searchTerm.ToLower();

            return await _context.StockItems
                .Where(s => s.IsActive &&
                    (s.Name.ToLower().Contains(searchTerm) ||
                     s.Description.ToLower().Contains(searchTerm)))
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<List<StockItem>> GetFeaturedItemsAsync()
        {
            return await _context.StockItems
                .Where(s => s.IsActive && s.IsFeatured)
                .OrderByDescending(s => s.Rating)
                .Take(6)
                .ToListAsync();
        }

        public async Task<List<StockItem>> GetBestSellersAsync(int count = 5)
        {
            return await _context.StockItems
                .Where(s => s.IsActive && s.UnitsSold > 0)
                .OrderByDescending(s => s.UnitsSold)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Dictionary<StockCategory, int>> GetStockStatisticsAsync()
        {
            var statistics = await _context.StockItems
                .Where(s => s.IsActive)
                .GroupBy(s => s.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Category, x => x.Count);

            // Ensure all categories are represented
            foreach (StockCategory category in Enum.GetValues(typeof(StockCategory)))
            {
                if (!statistics.ContainsKey(category))
                    statistics[category] = 0;
            }

            return statistics;
        }

        public async Task<bool> IsStockAvailableAsync(int id, int requestedQuantity)
        {
            var item = await GetStockByIdAsync(id);
            return item != null && item.IsActive && item.Quantity >= requestedQuantity;
        }
    }
}
// Claude Prompt 027 end