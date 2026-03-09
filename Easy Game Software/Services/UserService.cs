using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

// Claude Prompt 023 start
namespace Easy_Games_Software.Services
{
    /// <summary>
    /// Interface for user-related operations
    /// </summary>
    public interface IUserService
    {
        Task<User?> AuthenticateAsync(string username, string password);
        Task<User?> GetUserByIdAsync(int id);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<bool> RegisterUserAsync(User user);
        Task<bool> UpdateUserAsync(User user);
        Task<bool> DeleteUserAsync(int userId);
        Task<List<User>> GetAllUsersAsync();
        Task<bool> PromoteToOwnerAsync(int userId);
        Task<bool> UpdateUserPointsAsync(int userId, int points);
        Task<bool> CheckAndUpgradeTierAsync(int userId);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }

    /// <summary>
    /// User service implementation
    /// </summary>
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(ApplicationDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && u.IsActive);

                if (user != null && VerifyPassword(password, user.Password))
                {
                    user.LastLoginDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication for user {Username}", username);
                return null;
            }
        }

        // Fixed null reference by adding Include for related entities
        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .Include(u => u.Transactions)
                    .ThenInclude(t => t.StockItem)
                .Include(u => u.CartItems)
                    .ThenInclude(c => c.StockItem)
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && u.IsActive);
        }

        public async Task<bool> RegisterUserAsync(User user)
        {
            try
            {
                // Check if username already exists
                var existingUser = await GetUserByUsernameAsync(user.Username);
                if (existingUser != null)
                {
                    _logger.LogWarning("Registration failed: Username {Username} already exists", user.Username);
                    return false;
                }

                // Hash the password
                user.Password = HashPassword(user.Password);
                user.RegistrationDate = DateTime.Now;
                user.IsActive = true;
                user.Role = UserRole.User; // Default role
                user.Tier = UserTier.Silver; // Default tier
                user.Points = 0;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {Username} registered successfully", user.Username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user {Username}", user.Username);
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", user.Id);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null) return false;

                // Don't delete the last owner
                if (user.Role == UserRole.Owner)
                {
                    var ownerCount = await _context.Users.CountAsync(u => u.Role == UserRole.Owner && u.IsActive);
                    if (ownerCount <= 1)
                    {
                        _logger.LogWarning("Cannot delete the last owner account");
                        return false;
                    }
                }

                // Soft delete
                user.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} deleted successfully", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<bool> PromoteToOwnerAsync(int userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null || user.Role == UserRole.Owner)
                    return false;

                user.Role = UserRole.Owner;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} promoted to Owner", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error promoting user {UserId} to Owner", userId);
                return false;
            }
        }

        public async Task<bool> UpdateUserPointsAsync(int userId, int points)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null) return false;

                user.Points += points;
                await CheckAndUpgradeTierAsync(userId);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating points for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> CheckAndUpgradeTierAsync(int userId)
        {
            try
            {
                var user = await GetUserByIdAsync(userId);
                if (user == null) return false;

                var previousTier = user.Tier;
                user.UpdateTier();

                if (previousTier != user.Tier)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("User {UserId} upgraded from {OldTier} to {NewTier}",
                        userId, previousTier, user.Tier);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking tier upgrade for user {UserId}", userId);
                return false;
            }
        }

        public string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            string hashOfInput = HashPassword(password);
            return hashOfInput == hashedPassword;
        }
    }
}
// Claude Prompt 023 end