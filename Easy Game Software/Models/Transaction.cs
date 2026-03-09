using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// Claude Prompt 018 start
namespace Easy_Games_Software.Models
{
    /// <summary>
    /// Transaction entity representing completed purchases
    /// </summary>
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int StockItemId { get; set; }

        [Required]
        [Range(1, 100)]
        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [Required]
        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(10, 2)")]
        [Display(Name = "Unit Price")]
        public decimal UnitPrice { get; set; }

        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(10, 2)")]
        [Display(Name = "Discount Applied")]
        public decimal DiscountAmount { get; set; } = 0;

        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(10, 2)")]
        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Points Earned")]
        public int PointsEarned { get; set; }

        [Display(Name = "Transaction Date")]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [Display(Name = "Status")]
        public TransactionStatus Status { get; set; } = TransactionStatus.Completed;

        [StringLength(50)]
        [Display(Name = "Transaction Reference")]
        public string? TransactionReference { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("StockItemId")]
        public virtual StockItem StockItem { get; set; } = null!;

        public int? ShopId { get; set; }

        [ForeignKey("ShopId")]
        public virtual Shop? Shop { get; set; }

        // Helper methods
        public void CalculateTotals(decimal discountRate = 0)
        {
            var subtotal = UnitPrice * Quantity;
            DiscountAmount = subtotal * discountRate;
            TotalAmount = subtotal - DiscountAmount;
        }

        public void CalculatePoints(int pointsMultiplier = 1)
        {
            // 1 point per $10 spent (multiplied by tier multiplier)
            PointsEarned = (int)(TotalAmount / 10) * pointsMultiplier;
        }

        public string GenerateReference()
        {
            TransactionReference = $"TXN-{DateTime.Now:yyyyMMdd}-{Id:D6}";
            return TransactionReference;
        }
    }
}
// Claude Prompt 018 end