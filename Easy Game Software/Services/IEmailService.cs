using Easy_Games_Software.Models;

// Claude Prompt 029 start
namespace Easy_Games_Software.Services
{
    /// <summary>
    /// Interface for email service operations
    /// </summary>
    public interface IEmailService
    {
        Task<bool> SendNewStockNotificationAsync(StockItem stockItem, List<User> customers);
        Task<bool> SendEmailAsync(string toEmail, string subject, string body);
        Task<bool> SendBulkEmailAsync(List<string> toEmails, string subject, string body);
        Task<bool> SendPromotionalEmailAsync(List<User> users, string subject, string message);
        Task<bool> SendEmailToTierAsync(UserTier tier, string subject, string message);
        Task<bool> SendEmailToUserAsync(int userId, string subject, string message);
        Task<bool> SendEmailToAllCustomersAsync(string subject, string message);
    }
}
// Claude Prompt 029 end