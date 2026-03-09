using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// Claude Prompt 017 start
namespace Easy_Games_Software.Models
{
    /// <summary>
    /// Shopping cart item entity
    /// </summary>
    public class CartItem
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
        public int Quantity { get; set; } = 1;

        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("StockItemId")]
        public virtual StockItem StockItem { get; set; } = null!;

        // Helper methods
        [NotMapped]
        public decimal Subtotal => StockItem?.Price * Quantity ?? 0;

        [NotMapped]
        public decimal DiscountedSubtotal => Subtotal * (1 - (User?.GetDiscountRate() ?? 0));

        public bool IsAvailable() => StockItem?.IsInStock() ?? false;

        public bool HasSufficientStock() => StockItem?.Quantity >= Quantity;
    }
}
// Claude Prompt 017 end