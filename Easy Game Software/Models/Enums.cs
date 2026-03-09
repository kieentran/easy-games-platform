// Claude Prompt 021 start
namespace Easy_Games_Software.Models
{
    /// <summary>
    /// User roles in the system
    /// </summary>
    public enum UserRole
    {
        User = 0,
        Owner = 1,
        ShopProprietor = 2
    }

    /// <summary>
    /// User tier for rewards system
    /// Bronze: 0-49 points (1x multiplier, 0% discount)
    /// Silver: 50-99 points (1x multiplier, 5% discount)
    /// Gold: 100-249 points (2x multiplier, 10% discount)
    /// Platinum: 250+ points (3x multiplier, 15% discount)
    /// </summary>
    public enum UserTier
    {
        Bronze = 0,
        Silver = 1,
        Gold = 2,
        Platinum = 3
    }

    /// <summary>
    /// Stock item categories
    /// </summary>
    public enum StockCategory
    {
        Book,
        Game,
        Toy,
        Action,
        RPG,
        Puzzle,
        Sports,
        Strategy
    }

    /// <summary>
    /// Transaction status
    /// </summary>
    public enum TransactionStatus
    {
        Pending,
        Completed,
        Refunded,
        Cancelled
    }
}
// Claude Prompt 021 end