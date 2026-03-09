using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Microsoft.EntityFrameworkCore;

// Claude Prompt 024 start
namespace Easy_Games_Software.Services
{
    /// <summary>
    /// Interface for reward and tier management
    /// </summary>
    public interface IRewardService
    {
        int CalculatePoints(decimal amount, UserTier tier);
        decimal CalculateDiscount(decimal amount, UserTier tier);
        Task<bool> UpdateUserTierAsync(int userId);
        Task<int> GetPointsToNextTierAsync(int userId);
        Task<List<User>> GetGoldTierUsersAsync();
        Task<List<User>> GetPlatinumTierUsersAsync();
        Task<List<User>> GetUsersByTierAsync(UserTier tier);
        Task<Dictionary<UserTier, int>> GetTierDistributionAsync();
        Task<decimal> GetTotalPointsAwardedAsync();
        Task<decimal> GetTotalDiscountsGivenAsync();
    }

    /// <summary>
    /// Reward service implementation with 4-tier system
    /// Bronze: 0-49 points (1x multiplier, 0% discount)
    /// Silver: 50-99 points (1x multiplier, 5% discount)
    /// Gold: 100-249 points (2x multiplier, 10% discount)
    /// Platinum: 250+ points (3x multiplier, 15% discount)
    /// </summary>
    public class RewardService : IRewardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RewardService> _logger;

        // Tier thresholds
        private const int BRONZE_THRESHOLD = 0;
        private const int SILVER_THRESHOLD = 50;
        private const int GOLD_THRESHOLD = 100;
        private const int PLATINUM_THRESHOLD = 250;

        // Points calculation
        private const decimal POINTS_PER_DOLLAR = 0.1m; // 1 point per $10

        // Tier multipliers
        private const int BRONZE_MULTIPLIER = 1;
        private const int SILVER_MULTIPLIER = 1;
        private const int GOLD_MULTIPLIER = 2;
        private const int PLATINUM_MULTIPLIER = 3;

        // Discount rates
        private const decimal BRONZE_DISCOUNT_RATE = 0m;      // 0%
        private const decimal SILVER_DISCOUNT_RATE = 0.05m;   // 5%
        private const decimal GOLD_DISCOUNT_RATE = 0.10m;     // 10%
        private const decimal PLATINUM_DISCOUNT_RATE = 0.15m; // 15%

        public RewardService(ApplicationDbContext context, ILogger<RewardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Calculate points earned based on amount spent and tier
        /// </summary>
        public int CalculatePoints(decimal amount, UserTier tier)
        {
            int basePoints = (int)(amount * POINTS_PER_DOLLAR);

            int multiplier = tier switch
            {
                UserTier.Bronze => BRONZE_MULTIPLIER,
                UserTier.Silver => SILVER_MULTIPLIER,
                UserTier.Gold => GOLD_MULTIPLIER,
                UserTier.Platinum => PLATINUM_MULTIPLIER,
                _ => 1
            };

            int totalPoints = basePoints * multiplier;

            _logger.LogDebug("Calculated {Points} points for ${Amount} at {Tier} tier (multiplier: {Multiplier}x)",
                totalPoints, amount, tier, multiplier);

            return totalPoints;
        }

        /// <summary>
        /// Calculate discount based on amount and tier
        /// </summary>
        public decimal CalculateDiscount(decimal amount, UserTier tier)
        {
            decimal discountRate = tier switch
            {
                UserTier.Bronze => BRONZE_DISCOUNT_RATE,
                UserTier.Silver => SILVER_DISCOUNT_RATE,
                UserTier.Gold => GOLD_DISCOUNT_RATE,
                UserTier.Platinum => PLATINUM_DISCOUNT_RATE,
                _ => 0m
            };

            decimal discount = amount * discountRate;

            if (discount > 0)
            {
                _logger.LogDebug("Applied {Rate}% discount: ${Discount} off ${Amount} for {Tier} tier",
                    discountRate * 100, discount, amount, tier);
            }

            return discount;
        }

        /// <summary>
        /// Update user tier based on current points
        /// </summary>
        public async Task<bool> UpdateUserTierAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found for tier update", userId);
                    return false;
                }

                var previousTier = user.Tier;
                var newTier = DetermineTier(user.Points);

                if (newTier != previousTier)
                {
                    user.Tier = newTier;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("User {UserId} tier changed from {OldTier} to {NewTier} with {Points} points",
                        userId, previousTier, newTier, user.Points);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tier for user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Determine tier based on points
        /// </summary>
        private UserTier DetermineTier(int points)
        {
            if (points >= PLATINUM_THRESHOLD)
                return UserTier.Platinum;
            else if (points >= GOLD_THRESHOLD)
                return UserTier.Gold;
            else if (points >= SILVER_THRESHOLD)
                return UserTier.Silver;
            else
                return UserTier.Bronze;
        }

        /// <summary>
        /// Get points needed to reach next tier
        /// </summary>
        public async Task<int> GetPointsToNextTierAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return 0;

            return user.Tier switch
            {
                UserTier.Bronze => Math.Max(0, SILVER_THRESHOLD - user.Points),
                UserTier.Silver => Math.Max(0, GOLD_THRESHOLD - user.Points),
                UserTier.Gold => Math.Max(0, PLATINUM_THRESHOLD - user.Points),
                UserTier.Platinum => 0, // Already at max tier
                _ => 0
            };
        }

        /// <summary>
        /// Get all Gold tier users
        /// </summary>
        public async Task<List<User>> GetGoldTierUsersAsync()
        {
            return await _context.Users
                .Where(u => u.Tier == UserTier.Gold && u.IsActive)
                .OrderByDescending(u => u.Points)
                .ToListAsync();
        }

        /// <summary>
        /// Get all Platinum tier users
        /// </summary>
        public async Task<List<User>> GetPlatinumTierUsersAsync()
        {
            return await _context.Users
                .Where(u => u.Tier == UserTier.Platinum && u.IsActive)
                .OrderByDescending(u => u.Points)
                .ToListAsync();
        }

        /// <summary>
        /// Get all users by specific tier
        /// </summary>
        public async Task<List<User>> GetUsersByTierAsync(UserTier tier)
        {
            return await _context.Users
                .Where(u => u.Tier == tier && u.IsActive)
                .OrderByDescending(u => u.Points)
                .ToListAsync();
        }

        /// <summary>
        /// Get distribution of users across all tiers
        /// </summary>
        public async Task<Dictionary<UserTier, int>> GetTierDistributionAsync()
        {
            var distribution = await _context.Users
                .Where(u => u.IsActive && u.Role == UserRole.User)
                .GroupBy(u => u.Tier)
                .Select(g => new { Tier = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Tier, x => x.Count);

            // Ensure all tiers are represented
            foreach (UserTier tier in Enum.GetValues(typeof(UserTier)))
            {
                if (!distribution.ContainsKey(tier))
                    distribution[tier] = 0;
            }

            return distribution;
        }

        /// <summary>
        /// Get total points awarded across all transactions
        /// </summary>
        public async Task<decimal> GetTotalPointsAwardedAsync()
        {
            return await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .SumAsync(t => t.PointsEarned);
        }

        /// <summary>
        /// Get total discount amount given across all transactions
        /// </summary>
        public async Task<decimal> GetTotalDiscountsGivenAsync()
        {
            return await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .SumAsync(t => t.DiscountAmount);
        }
    }
}
// Claude Prompt 024 end