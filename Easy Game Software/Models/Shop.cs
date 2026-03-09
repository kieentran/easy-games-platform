using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// Claude Prompt 019 start
namespace Easy_Games_Software.Models
{
    /// <summary>
    /// Shop entity representing physical store locations
    /// AI Reference: Claude - Shop management system for brick-and-mortar stores
    /// </summary>
    public class Shop
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Shop Name")]
        public string ShopName { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "Location/Address")]
        public string Location { get; set; } = string.Empty;

        [StringLength(20)]
        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Date Opened")]
        public DateTime DateOpened { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Shop Proprietor")]
        public int ShopProprietorId { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey("ShopProprietorId")]
        public virtual User? ShopProprietor { get; set; }

        public virtual ICollection<ShopStock> ShopStocks { get; set; } = new List<ShopStock>();
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

        // Helper methods
        public int GetTotalInventoryCount()
        {
            return ShopStocks.Sum(ss => ss.QuantityInShop);
        }

        public decimal GetTotalInventoryValue()
        {
            return ShopStocks.Sum(ss => ss.QuantityInShop * ss.StockItem.Price);
        }
    }
}
// Claude Prompt 019 end