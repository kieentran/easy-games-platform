using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Microsoft.EntityFrameworkCore;

// Claude Prompt 026 start
namespace Easy_Games_Software.Services
{
    /// <summary>
    /// Interface for shop management operations
    /// </summary>
    public interface IShopService
    {
        Task<List<Shop>> GetAllShopsAsync(bool activeOnly = true);
        Task<Shop?> GetShopByIdAsync(int id);
        Task<Shop?> GetShopByProprietorIdAsync(int proprietorId);
        Task<bool> CreateShopAsync(Shop shop);
        Task<bool> UpdateShopAsync(Shop shop);
        Task<bool> DeleteShopAsync(int id);
        Task<List<Shop>> SearchShopsAsync(string searchTerm);
        Task<Dictionary<string, object>> GetShopStatisticsAsync(int shopId);
    }

    /// <summary>
    /// Shop service implementation
    /// AI Reference: Claude - Shop management service for POS system
    /// </summary>
    public class ShopService : IShopService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ShopService> _logger;

        public ShopService(ApplicationDbContext context, ILogger<ShopService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Shop>> GetAllShopsAsync(bool activeOnly = true)
        {
            var query = _context.Shops
                .Include(s => s.ShopProprietor)
                .AsQueryable();

            if (activeOnly)
                query = query.Where(s => s.IsActive);

            return await query.OrderBy(s => s.ShopName).ToListAsync();
        }

        public async Task<Shop?> GetShopByIdAsync(int id)
        {
            return await _context.Shops
                .Include(s => s.ShopProprietor)
                .Include(s => s.ShopStocks)
                    .ThenInclude(ss => ss.StockItem)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Shop?> GetShopByProprietorIdAsync(int proprietorId)
        {
            return await _context.Shops
                .Include(s => s.ShopStocks)
                    .ThenInclude(ss => ss.StockItem)
                .FirstOrDefaultAsync(s => s.ShopProprietorId == proprietorId && s.IsActive);
        }

        public async Task<bool> CreateShopAsync(Shop shop)
        {
            try
            {
                shop.DateOpened = DateTime.Now;
                shop.IsActive = true;

                _context.Shops.Add(shop);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Shop {ShopName} created successfully", shop.ShopName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shop {ShopName}", shop.ShopName);
                return false;
            }
        }

        public async Task<bool> UpdateShopAsync(Shop shop)
        {
            try
            {
                _context.Shops.Update(shop);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Shop {ShopId} updated successfully", shop.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shop {ShopId}", shop.Id);
                return false;
            }
        }

        public async Task<bool> DeleteShopAsync(int id)
        {
            try
            {
                var shop = await GetShopByIdAsync(id);
                if (shop == null) return false;

                // Soft delete
                shop.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Shop {ShopId} deactivated successfully", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shop {ShopId}", id);
                return false;
            }
        }

        public async Task<List<Shop>> SearchShopsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Shop>();

            searchTerm = searchTerm.ToLower();

            return await _context.Shops
                .Include(s => s.ShopProprietor)
                .Where(s => s.IsActive &&
                    (s.ShopName.ToLower().Contains(searchTerm) ||
                     s.Location.ToLower().Contains(searchTerm)))
                .OrderBy(s => s.ShopName)
                .ToListAsync();
        }

        public async Task<Dictionary<string, object>> GetShopStatisticsAsync(int shopId)
        {
            var shop = await GetShopByIdAsync(shopId);
            if (shop == null)
                return new Dictionary<string, object>();

            var stats = new Dictionary<string, object>();

            // Total inventory count and value
            stats["TotalItems"] = shop.ShopStocks.Sum(ss => ss.QuantityInShop);
            stats["TotalValue"] = shop.ShopStocks.Sum(ss => ss.TotalValue);

            // Low stock items
            stats["LowStockItems"] = shop.ShopStocks.Count(ss => ss.IsLowStock());

            // Out of stock items
            stats["OutOfStockItems"] = shop.ShopStocks.Count(ss => ss.QuantityInShop == 0);

            // Total sales
            var salesTransactions = await _context.Transactions
                .Where(t => t.ShopId == shopId && t.Status == TransactionStatus.Completed)
                .ToListAsync();

            stats["TotalSales"] = salesTransactions.Sum(t => t.TotalAmount);
            stats["TotalTransactions"] = salesTransactions.Count;

            return stats;
        }
    }
}
// Claude Prompt 026 end