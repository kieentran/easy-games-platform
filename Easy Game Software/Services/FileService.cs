using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

// Claude Prompt 028 start
namespace Easy_Games_Software.Services
{
    /// <summary>
    /// Interface for file import/export operations
    /// </summary>
    public interface IFileService
    {
        Task<bool> ExportUsersToCSVAsync(string filePath);
        Task<bool> ExportStockToCSVAsync(string filePath);
        Task<bool> ExportTransactionsToCSVAsync(string filePath);
        Task<bool> ImportStockFromCSVAsync(string filePath);
        Task<byte[]> GenerateSalesReportAsync(DateTime startDate, DateTime endDate);
        Task<bool> BackupDatabaseAsync(string backupPath);
        Task<bool> RestoreDatabaseAsync(string backupPath);
    }

    /// <summary>
    /// File service implementation for data import/export
    /// </summary>
    public class FileService : IFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FileService> _logger;

        public FileService(ApplicationDbContext context, ILogger<FileService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ExportUsersToCSVAsync(string filePath)
        {
            try
            {
                var users = await _context.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Id,Username,Email,FullName,Role,Points,Tier,TotalSpent,RegistrationDate");

                foreach (var user in users)
                {
                    csv.AppendLine($"{user.Id},{user.Username},{user.Email},{user.FullName}," +
                        $"{user.Role},{user.Points},{user.Tier},{user.TotalSpent:F2}," +
                        $"{user.RegistrationDate:yyyy-MM-dd}");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString());

                _logger.LogInformation("Exported {Count} users to {FilePath}", users.Count, filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting users to CSV");
                return false;
            }
        }

        public async Task<bool> ExportStockToCSVAsync(string filePath)
        {
            try
            {
                var items = await _context.StockItems
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Id,Name,Category,Price,Quantity,UnitsSold,Rating,DateAdded");

                foreach (var item in items)
                {
                    csv.AppendLine($"{item.Id},{EscapeCSV(item.Name)},{item.Category}," +
                        $"{item.Price:F2},{item.Quantity},{item.UnitsSold},{item.Rating:F1}," +
                        $"{item.DateAdded:yyyy-MM-dd}");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString());

                _logger.LogInformation("Exported {Count} stock items to {FilePath}", items.Count, filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting stock to CSV");
                return false;
            }
        }

        public async Task<bool> ExportTransactionsToCSVAsync(string filePath)
        {
            try
            {
                var transactions = await _context.Transactions
                    .Include(t => t.User)
                    .Include(t => t.StockItem)
                    .Where(t => t.Status == TransactionStatus.Completed)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Id,TransactionReference,Username,ItemName,Quantity,UnitPrice,DiscountAmount," +
                    "TotalAmount,PointsEarned,TransactionDate");

                foreach (var trans in transactions)
                {
                    csv.AppendLine($"{trans.Id},{trans.TransactionReference},{trans.User.Username}," +
                        $"{EscapeCSV(trans.StockItem.Name)},{trans.Quantity},{trans.UnitPrice:F2}," +
                        $"{trans.DiscountAmount:F2},{trans.TotalAmount:F2},{trans.PointsEarned}," +
                        $"{trans.TransactionDate:yyyy-MM-dd HH:mm:ss}");
                }

                await File.WriteAllTextAsync(filePath, csv.ToString());

                _logger.LogInformation("Exported {Count} transactions to {FilePath}", transactions.Count, filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting transactions to CSV");
                return false;
            }
        }

        public async Task<bool> ImportStockFromCSVAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Import file not found: {FilePath}", filePath);
                    return false;
                }

                var lines = await File.ReadAllLinesAsync(filePath);
                if (lines.Length <= 1) return false; // No data or only header

                var importedCount = 0;

                // Skip header row
                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length < 4) continue; // Minimum required fields

                    var item = new StockItem
                    {
                        Name = UnescapeCSV(parts[0]),
                        Category = Enum.TryParse<StockCategory>(parts[1], out var cat) ? cat : StockCategory.Game,
                        Price = decimal.TryParse(parts[2], out var price) ? price : 0,
                        Quantity = int.TryParse(parts[3], out var qty) ? qty : 0,
                        Description = parts.Length > 4 ? UnescapeCSV(parts[4]) : "",
                        DateAdded = DateTime.Now,
                        IsActive = true
                    };

                    if (item.Price > 0 && !string.IsNullOrEmpty(item.Name))
                    {
                        _context.StockItems.Add(item);
                        importedCount++;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Imported {Count} stock items from {FilePath}", importedCount, filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing stock from CSV");
                return false;
            }
        }

        public async Task<byte[]> GenerateSalesReportAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var transactions = await _context.Transactions
                    .Include(t => t.User)
                    .Include(t => t.StockItem)
                    .Where(t => t.Status == TransactionStatus.Completed &&
                               t.TransactionDate >= startDate &&
                               t.TransactionDate <= endDate)
                    .OrderBy(t => t.TransactionDate)
                    .ToListAsync();

                var report = new StringBuilder();
                report.AppendLine($"EASY GAMES SALES REPORT");
                report.AppendLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine(new string('=', 80));
                report.AppendLine();

                // Summary statistics
                var totalRevenue = transactions.Sum(t => t.TotalAmount);
                var totalDiscounts = transactions.Sum(t => t.DiscountAmount);
                var totalPoints = transactions.Sum(t => t.PointsEarned);
                var uniqueCustomers = transactions.Select(t => t.UserId).Distinct().Count();

                report.AppendLine("SUMMARY");
                report.AppendLine($"Total Transactions: {transactions.Count}");
                report.AppendLine($"Unique Customers: {uniqueCustomers}");
                report.AppendLine($"Total Revenue: ${totalRevenue:F2}");
                report.AppendLine($"Total Discounts: ${totalDiscounts:F2}");
                report.AppendLine($"Total Points Awarded: {totalPoints}");
                report.AppendLine($"Average Transaction Value: ${(transactions.Any() ? totalRevenue / transactions.Count : 0):F2}");
                report.AppendLine();

                // Top selling items
                report.AppendLine("TOP SELLING ITEMS");
                var topItems = transactions
                    .GroupBy(t => t.StockItem.Name)
                    .Select(g => new { Name = g.Key, Quantity = g.Sum(t => t.Quantity), Revenue = g.Sum(t => t.TotalAmount) })
                    .OrderByDescending(x => x.Revenue)
                    .Take(5);

                foreach (var item in topItems)
                {
                    report.AppendLine($"- {item.Name}: {item.Quantity} units, ${item.Revenue:F2}");
                }
                report.AppendLine();

                // Daily breakdown
                report.AppendLine("DAILY BREAKDOWN");
                var dailyStats = transactions
                    .GroupBy(t => t.TransactionDate.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count(), Revenue = g.Sum(t => t.TotalAmount) })
                    .OrderBy(x => x.Date);

                foreach (var day in dailyStats)
                {
                    report.AppendLine($"{day.Date:yyyy-MM-dd}: {day.Count} transactions, ${day.Revenue:F2}");
                }

                return Encoding.UTF8.GetBytes(report.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sales report");
                return Encoding.UTF8.GetBytes("Error generating report");
            }
        }

        public async Task<bool> BackupDatabaseAsync(string backupPath)
        {
            try
            {
                var connectionString = _context.Database.GetConnectionString();
                if (connectionString != null && connectionString.Contains("Data Source="))
                {
                    var dbPath = connectionString.Split("Data Source=")[1].Split(';')[0];
                    if (File.Exists(dbPath))
                    {
                        // Make it async
                        await Task.Run(() => File.Copy(dbPath, backupPath, overwrite: true));
                        _logger.LogInformation("Database backed up to {BackupPath}", backupPath);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error backing up database");
                return false;
            }
        }

        public async Task<bool> RestoreDatabaseAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    _logger.LogWarning("Backup file not found: {BackupPath}", backupPath);
                    return false;
                }

                // For SQLite, restore by copying the backup file
                var connectionString = _context.Database.GetConnectionString();
                if (connectionString != null && connectionString.Contains("Data Source="))
                {
                    var dbPath = connectionString.Split("Data Source=")[1].Split(';')[0];

                    // Close current connection
                    await _context.Database.CloseConnectionAsync();

                    // Replace database file
                    File.Copy(backupPath, dbPath, overwrite: true);

                    _logger.LogInformation("Database restored from {BackupPath}", backupPath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring database");
                return false;
            }
        }

        private string EscapeCSV(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private string UnescapeCSV(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Substring(1, value.Length - 2);
                return value.Replace("\"\"", "\"");
            }

            return value;
        }
    }
}
// Claude Prompt 028 end