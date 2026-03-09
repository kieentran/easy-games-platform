using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Microsoft.EntityFrameworkCore;

// Claude Prompt 025 start
namespace Easy_Games_Software.Services
{
    /// <summary>
    /// Interface for sales and transaction operations
    /// </summary>
    public interface ISalesService
    {
        Task<Transaction?> ProcessPurchaseAsync(int userId, List<CartItem> cartItems);
        Task<List<Transaction>> GetUserTransactionsAsync(int userId);
        Task<List<Transaction>> GetAllTransactionsAsync();
        Task<Transaction?> GetTransactionByIdAsync(int id);
        Task<bool> RefundTransactionAsync(int transactionId);
        Task<decimal> GetTotalRevenueAsync();
        Task<decimal> GetRevenueByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<Transaction>> GetRecentTransactionsAsync(int count = 10);
        Task<Dictionary<string, decimal>> GetSalesStatisticsAsync();
        Task<List<(User user, decimal totalSpent)>> GetTopCustomersAsync(int count = 5);
    }

    /// <summary>
    /// Sales service implementation
    /// </summary>
    public class SalesService : ISalesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IStockService _stockService;
        private readonly IRewardService _rewardService;
        private readonly ILogger<SalesService> _logger;

        public SalesService(
            ApplicationDbContext context,
            IUserService userService,
            IStockService stockService,
            IRewardService rewardService,
            ILogger<SalesService> logger)
        {
            _context = context;
            _userService = userService;
            _stockService = stockService;
            _rewardService = rewardService;
            _logger = logger;
        }

        public async Task<Transaction?> ProcessPurchaseAsync(int userId, List<CartItem> cartItems)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError("User {UserId} not found", userId);
                    return null;
                }

                var transactions = new List<Transaction>();
                decimal totalAmount = 0;
                int totalPoints = 0;

                foreach (var cartItem in cartItems)
                {
                    // Get the stock item
                    var stockItem = await _stockService.GetStockByIdAsync(cartItem.StockItemId);
                    if (stockItem == null || !stockItem.IsInStock() || stockItem.Quantity < cartItem.Quantity)
                    {
                        _logger.LogWarning("Stock item {ItemId} not available", cartItem.StockItemId);
                        await transaction.RollbackAsync();
                        return null;
                    }

                    // Create transaction
                    var trans = new Transaction
                    {
                        UserId = userId,
                        StockItemId = stockItem.Id,
                        Quantity = cartItem.Quantity,
                        UnitPrice = stockItem.Price,
                        TransactionDate = DateTime.Now,
                        Status = TransactionStatus.Completed
                    };

                    // Calculate totals with discount
                    trans.CalculateTotals(user.GetDiscountRate());
                    trans.CalculatePoints(user.GetPointsMultiplier());
                    trans.GenerateReference();

                    // Update stock
                    stockItem.UpdateStock(cartItem.Quantity);
                    _context.StockItems.Update(stockItem);

                    // Add transaction
                    _context.Transactions.Add(trans);
                    transactions.Add(trans);

                    totalAmount += trans.TotalAmount;
                    totalPoints += trans.PointsEarned;
                }

                // Update user points and spending
                user.Points += totalPoints;
                user.TotalSpent += totalAmount;
                user.UpdateTier();
                _context.Users.Update(user);

                // Clear the user's cart
                var userCartItems = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .ToListAsync();
                _context.CartItems.RemoveRange(userCartItems);

                // Save all changes
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Purchase completed for user {UserId}. Total: {Total}, Points: {Points}",
                    userId, totalAmount, totalPoints);

                // Return the first transaction as representative
                return transactions.FirstOrDefault();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing purchase for user {UserId}", userId);
                return null;
            }
        }

        public async Task<List<Transaction>> GetUserTransactionsAsync(int userId)
        {
            return await _context.Transactions
                .Include(t => t.StockItem)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetAllTransactionsAsync()
        {
            return await _context.Transactions
                .Include(t => t.User)
                .Include(t => t.StockItem)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<Transaction?> GetTransactionByIdAsync(int id)
        {
            return await _context.Transactions
                .Include(t => t.User)
                .Include(t => t.StockItem)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<bool> RefundTransactionAsync(int transactionId)
        {
            try
            {
                var trans = await GetTransactionByIdAsync(transactionId);
                if (trans == null || trans.Status != TransactionStatus.Completed)
                    return false;

                // Restore stock
                var stockItem = await _stockService.GetStockByIdAsync(trans.StockItemId);
                if (stockItem != null)
                {
                    stockItem.Quantity += trans.Quantity;
                    stockItem.UnitsSold -= trans.Quantity;
                    _context.StockItems.Update(stockItem);
                }

                // Reverse points and spending
                var user = await _userService.GetUserByIdAsync(trans.UserId);
                if (user != null)
                {
                    user.Points = Math.Max(0, user.Points - trans.PointsEarned);
                    user.TotalSpent = Math.Max(0, user.TotalSpent - trans.TotalAmount);

                    // Check if tier downgrade needed
                    if (user.Tier == UserTier.Gold && user.Points < 100)
                    {
                        user.Tier = UserTier.Silver;
                    }

                    _context.Users.Update(user);
                }

                // Update transaction status
                trans.Status = TransactionStatus.Refunded;
                trans.Notes = $"Refunded on {DateTime.Now:yyyy-MM-dd HH:mm}";
                _context.Transactions.Update(trans);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Transaction {TransactionId} refunded successfully", transactionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding transaction {TransactionId}", transactionId);
                return false;
            }
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            return await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .SumAsync(t => t.TotalAmount);
        }

        public async Task<decimal> GetRevenueByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed &&
                           t.TransactionDate >= startDate &&
                           t.TransactionDate <= endDate)
                .SumAsync(t => t.TotalAmount);
        }

        public async Task<List<Transaction>> GetRecentTransactionsAsync(int count = 10)
        {
            return await _context.Transactions
                .Include(t => t.User)
                .Include(t => t.StockItem)
                .Where(t => t.Status == TransactionStatus.Completed)
                .OrderByDescending(t => t.TransactionDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Dictionary<string, decimal>> GetSalesStatisticsAsync()
        {
            var stats = new Dictionary<string, decimal>();

            // Get completed transactions first
            var completedTransactions = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .ToListAsync();

            // Total revenue
            stats["TotalRevenue"] = completedTransactions.Sum(t => t.TotalAmount);

            // Total transactions
            stats["TotalTransactions"] = completedTransactions.Count;

            // Average transaction value
            if (stats["TotalTransactions"] > 0)
            {
                stats["AverageTransactionValue"] = stats["TotalRevenue"] / stats["TotalTransactions"];
            }
            else
            {
                stats["AverageTransactionValue"] = 0;
            }

            // Total discount given
            stats["TotalDiscounts"] = completedTransactions.Sum(t => t.DiscountAmount);

            // Total points awarded
            stats["TotalPointsAwarded"] = completedTransactions.Sum(t => t.PointsEarned);

            // Revenue this month
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var thisMonthTransactions = completedTransactions
                .Where(t => t.TransactionDate >= startOfMonth && t.TransactionDate <= DateTime.Now)
                .ToList();

            stats["RevenueThisMonth"] = thisMonthTransactions.Sum(t => t.TotalAmount);

            return stats;
        }

        // Fixed SQLite decimal ordering by fetching data first
        public async Task<List<(User user, decimal totalSpent)>> GetTopCustomersAsync(int count = 5)
        {
            // Fetch users first, then order in memory to avoid SQLite decimal ordering issue
            var users = await _context.Users
                .Where(u => u.IsActive && u.Role == UserRole.User)
                .ToListAsync();

            var topCustomers = users
                .OrderByDescending(u => u.TotalSpent)
                .Take(count)
                .Select(u => (user: u, totalSpent: u.TotalSpent))
                .ToList();

            return topCustomers;
        }
    }
}
// Claude Prompt 025 end