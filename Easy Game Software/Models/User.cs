using System.ComponentModel.DataAnnotations;

// Claude Prompt 015 start
namespace Easy_Games_Software.Models
{
    /// <summary>
    /// User entity representing system users (Owner or Customer)
    /// </summary>
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty; // In production, this should be hashed

        [Required]
        public UserRole Role { get; set; } = UserRole.User;

        [Display(Name = "Reward Points")]
        public int Points { get; set; } = 0;

        [Display(Name = "Membership Tier")]
        public UserTier Tier { get; set; } = UserTier.Bronze;

        [Display(Name = "Email Address")]
        [EmailAddress]
        public string? Email { get; set; }

        [Display(Name = "Full Name")]
        [StringLength(100)]
        public string? FullName { get; set; }

        [Display(Name = "Phone Number")]
        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Registration Date")]
        public DateTime RegistrationDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Login")]
        public DateTime? LastLoginDate { get; set; }

        [Display(Name = "Total Spent")]
        [DataType(DataType.Currency)]
        public decimal TotalSpent { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

        // Helper methods
        public bool IsOwner() => Role == UserRole.Owner;

        public bool IsGoldTier() => Tier == UserTier.Gold;

        public bool IsPlatinumTier() => Tier == UserTier.Platinum;

        /// <summary>
        /// Get points multiplier based on tier
        /// Bronze: 1x, Silver: 1x, Gold: 2x, Platinum: 3x
        /// </summary>
        public int GetPointsMultiplier()
        {
            return Tier switch
            {
                UserTier.Bronze => 1,
                UserTier.Silver => 1,
                UserTier.Gold => 2,
                UserTier.Platinum => 3,
                _ => 1
            };
        }

        /// <summary>
        /// Get discount rate based on tier
        /// Bronze: 0%, Silver: 5%, Gold: 10%, Platinum: 15%
        /// </summary>
        public decimal GetDiscountRate()
        {
            return Tier switch
            {
                UserTier.Bronze => 0m,
                UserTier.Silver => 0.05m,
                UserTier.Gold => 0.10m,
                UserTier.Platinum => 0.15m,
                _ => 0m
            };
        }

        /// <summary>
        /// Update user tier based on points
        /// Bronze: 0-49, Silver: 50-99, Gold: 100-249, Platinum: 250+
        /// </summary>
        public void UpdateTier()
        {
            if (Points >= 250)
            {
                Tier = UserTier.Platinum;
            }
            else if (Points >= 100)
            {
                Tier = UserTier.Gold;
            }
            else if (Points >= 50)
            {
                Tier = UserTier.Silver;
            }
            else
            {
                Tier = UserTier.Bronze;
            }
        }

        /// <summary>
        /// Get the name of the current tier
        /// </summary>
        public string GetTierName() => Tier.ToString();

        /// <summary>
        /// Get points needed to reach next tier
        /// </summary>
        public int GetPointsToNextTier()
        {
            return Tier switch
            {
                UserTier.Bronze => Math.Max(0, 50 - Points),
                UserTier.Silver => Math.Max(0, 100 - Points),
                UserTier.Gold => Math.Max(0, 250 - Points),
                UserTier.Platinum => 0, // Already at max tier
                _ => 0
            };
        }
    }
}
// Claude Prompt 015 end