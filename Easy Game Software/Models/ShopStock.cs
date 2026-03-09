using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// Claude Prompt 020 start
namespace Easy_Games_Software.Models
{
    /// <summary>
    /// ShopStock entity - links shops to inventory items
    /// AI Reference: Claude - Separate inventory tracking for physical shops
    /// </summary>
    public class ShopStock
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ShopId { get; set; }

        [Required]
        public int StockItemId { get; set; }

        [Required]
        [Range(0, 10000)]
        [Display(Name = "Quantity in Shop")]
        public int QuantityInShop { get; set; } = 0;

        [Display(Name = "Last Restocked")]
        public DateTime? LastRestocked { get; set; }

        [Display(Name = "Date Added to Shop")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("ShopId")]
        public virtual Shop Shop { get; set; } = null!;

        [ForeignKey("StockItemId")]
        public virtual StockItem StockItem { get; set; } = null!;

        // Helper methods
        public bool HasStock() => QuantityInShop > 0;

        public bool IsLowStock() => QuantityInShop > 0 && QuantityInShop <= 5;

        public void AddStock(int quantity)
        {
            QuantityInShop += quantity;
            LastRestocked = DateTime.Now;
        }

        public bool DeductStock(int quantity)
        {
            if (quantity > QuantityInShop)
                return false;

            QuantityInShop -= quantity;
            return true;
        }

        // Get price from parent stock item
        [NotMapped]
        public decimal Price => StockItem?.Price ?? 0;

        [NotMapped]
        public decimal TotalValue => QuantityInShop * Price;
    }
}
// Claude Prompt 020 end