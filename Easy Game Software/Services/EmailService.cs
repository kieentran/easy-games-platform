using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using System.Text;

// Claude Prompt 029 start
namespace Easy_Games_Software.Services
{
    /// <summary>
    /// Email service implementation with promotional and tier-based functionality
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger,
            ApplicationDbContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;

            // Load email settings from appsettings.json
            _smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["EmailSettings:SmtpUsername"] ?? "";
            _smtpPassword = _configuration["EmailSettings:SmtpPassword"] ?? "";
            _fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@easygames.com";
            _fromName = _configuration["EmailSettings:FromName"] ?? "Easy Games Software";
        }

        /// <summary>
        /// Send new stock notification to all customers
        /// </summary>
        public async Task<bool> SendNewStockNotificationAsync(StockItem stockItem, List<User> customers)
        {
            try
            {
                var customerEmails = customers
                    .Where(c => !string.IsNullOrEmpty(c.Email))
                    .Select(c => c.Email!)
                    .ToList();

                if (!customerEmails.Any())
                {
                    _logger.LogWarning("No customer emails found to send notification");
                    return false;
                }

                var subject = $"🎮 New Stock Alert: {stockItem.Name} Now Available!";
                var body = GenerateNewStockEmailBody(stockItem);

                return await SendBulkEmailAsync(customerEmails, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending new stock notification for {ItemName}", stockItem.Name);
                return false;
            }
        }

        /// <summary>
        /// Send promotional email to specific users
        /// </summary>
        public async Task<bool> SendPromotionalEmailAsync(List<User> users, string subject, string message)
        {
            try
            {
                var userEmails = users
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .Select(u => u.Email!)
                    .ToList();

                if (!userEmails.Any())
                {
                    _logger.LogWarning("No user emails found to send promotional email");
                    return false;
                }

                var body = GeneratePromotionalEmailBody(subject, message);
                return await SendBulkEmailAsync(userEmails, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending promotional email");
                return false;
            }
        }

        /// <summary>
        /// Send email to all users in a specific tier (Silver or Gold)
        /// </summary>
        public async Task<bool> SendEmailToTierAsync(UserTier tier, string subject, string message)
        {
            try
            {
                var users = await _context.Users
                    .Where(u => u.IsActive &&
                                u.Role == UserRole.User &&
                                u.Tier == tier &&
                                !string.IsNullOrEmpty(u.Email))
                    .ToListAsync();

                if (!users.Any())
                {
                    _logger.LogWarning("No users found in {Tier} tier with email addresses", tier);
                    return false;
                }

                _logger.LogInformation("Sending email to {Count} users in {Tier} tier", users.Count, tier);
                return await SendPromotionalEmailAsync(users, subject, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Tier} tier", tier);
                return false;
            }
        }

        /// <summary>
        /// Send email to a specific user by ID
        /// </summary>
        public async Task<bool> SendEmailToUserAsync(int userId, string subject, string message)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

                if (user == null || string.IsNullOrEmpty(user.Email))
                {
                    _logger.LogWarning("User {UserId} not found or has no email", userId);
                    return false;
                }

                var body = GeneratePromotionalEmailBody(subject, message);
                return await SendEmailAsync(user.Email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to user {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Send email to all customers (all users with User role)
        /// </summary>
        public async Task<bool> SendEmailToAllCustomersAsync(string subject, string message)
        {
            try
            {
                var users = await _context.Users
                    .Where(u => u.IsActive &&
                                u.Role == UserRole.User &&
                                !string.IsNullOrEmpty(u.Email))
                    .ToListAsync();

                if (!users.Any())
                {
                    _logger.LogWarning("No customers found with email addresses");
                    return false;
                }

                _logger.LogInformation("Sending email to all {Count} customers", users.Count);
                return await SendPromotionalEmailAsync(users, subject, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to all customers");
                return false;
            }
        }

        /// <summary>
        /// Send email to a single recipient
        /// </summary>
        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                using var message = new MailMessage();
                message.From = new MailAddress(_fromEmail, _fromName);
                message.To.Add(toEmail);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                await client.SendMailAsync(message);

                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {Email}", toEmail);
                return false;
            }
        }

        /// <summary>
        /// Send email to multiple recipients
        /// </summary>
        public async Task<bool> SendBulkEmailAsync(List<string> toEmails, string subject, string body)
        {
            try
            {
                using var message = new MailMessage();
                message.From = new MailAddress(_fromEmail, _fromName);

                // Add all recipients to BCC to protect privacy
                foreach (var email in toEmails)
                {
                    message.Bcc.Add(email);
                }

                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                await client.SendMailAsync(message);

                _logger.LogInformation("Bulk email sent successfully to {Count} recipients", toEmails.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bulk email to {Count} recipients", toEmails.Count);
                return false;
            }
        }

        /// <summary>
        /// Generate HTML email body for new stock notification
        /// </summary>
        private string GenerateNewStockEmailBody(StockItem stockItem)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }");
            sb.AppendLine(".container { max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
            sb.AppendLine(".header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; }");
            sb.AppendLine(".header h1 { margin: 0; font-size: 28px; }");
            sb.AppendLine(".content { padding: 30px; }");
            sb.AppendLine(".product-info { background-color: #f8f9fa; border-left: 4px solid #667eea; padding: 20px; margin: 20px 0; }");
            sb.AppendLine(".product-info h2 { color: #333; margin-top: 0; }");
            sb.AppendLine(".product-detail { margin: 10px 0; color: #666; }");
            sb.AppendLine(".product-detail strong { color: #333; }");
            sb.AppendLine(".price { font-size: 24px; color: #28a745; font-weight: bold; margin: 15px 0; }");
            sb.AppendLine(".button { display: inline-block; background-color: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin-top: 20px; }");
            sb.AppendLine(".button:hover { background-color: #764ba2; }");
            sb.AppendLine(".footer { background-color: #333; color: white; text-align: center; padding: 20px; font-size: 12px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class='container'>");

            // Header
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h1>🎮 New Stock Alert!</h1>");
            sb.AppendLine("<p>We've just added something exciting to our store!</p>");
            sb.AppendLine("</div>");

            // Content
            sb.AppendLine("<div class='content'>");
            sb.AppendLine("<p>Hey there!</p>");
            sb.AppendLine("<p>Great news! We've just added a brand new item to our inventory that we think you'll love:</p>");

            // Product Info
            sb.AppendLine("<div class='product-info'>");
            sb.AppendLine($"<h2>{stockItem.Name}</h2>");

            if (!string.IsNullOrEmpty(stockItem.Description))
            {
                sb.AppendLine($"<p>{stockItem.Description}</p>");
            }

            sb.AppendLine($"<div class='product-detail'><strong>Category:</strong> {stockItem.Category}</div>");
            sb.AppendLine($"<div class='product-detail'><strong>Available Quantity:</strong> {stockItem.Quantity} units</div>");
            sb.AppendLine($"<div class='price'>${stockItem.Price:F2}</div>");

            if (stockItem.IsFeatured)
            {
                sb.AppendLine("<div class='product-detail' style='color: #ff6b6b;'><strong>⭐ Featured Item!</strong></div>");
            }

            sb.AppendLine("</div>");

            sb.AppendLine("<p>Don't miss out on this new addition! Visit our store now to check it out and place your order before stock runs out.</p>");

            sb.AppendLine("<p style='margin-top: 30px; color: #666; font-size: 14px;'>Happy shopping!<br>The Easy Games Software Team</p>");
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p>&copy; 2025 Easy Games Software. All rights reserved.</p>");
            sb.AppendLine("<p>You're receiving this email because you're a valued customer.</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// Generate HTML email body for promotional messages
        /// </summary>
        private string GeneratePromotionalEmailBody(string subject, string message)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }");
            sb.AppendLine(".container { max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
            sb.AppendLine(".header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; }");
            sb.AppendLine(".header h1 { margin: 0; font-size: 28px; }");
            sb.AppendLine(".content { padding: 30px; line-height: 1.6; }");
            sb.AppendLine(".message-box { background-color: #f8f9fa; border-left: 4px solid #667eea; padding: 20px; margin: 20px 0; white-space: pre-wrap; }");
            sb.AppendLine(".footer { background-color: #333; color: white; text-align: center; padding: 20px; font-size: 12px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class='container'>");

            // Header
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h1>📧 Message from Easy Games Software</h1>");
            sb.AppendLine("</div>");

            // Content
            sb.AppendLine("<div class='content'>");
            sb.AppendLine("<p>Hello valued customer,</p>");
            sb.AppendLine($"<div class='message-box'>{System.Net.WebUtility.HtmlEncode(message)}</div>");
            sb.AppendLine("<p style='margin-top: 30px; color: #666; font-size: 14px;'>Best regards,<br>The Easy Games Software Team</p>");
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p>&copy; 2025 Easy Games Software. All rights reserved.</p>");
            sb.AppendLine("<p>You're receiving this email because you're a valued customer.</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
// Claude Prompt 029 end