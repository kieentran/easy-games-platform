using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// Claude Prompt 016 start
namespace Easy_Games_Software.Models
{
    /// <summary>
    /// Stock item entity representing products in the store
    /// </summary>
    public class StockItem
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(100, ErrorMessage = "Product name cannot exceed 100 characters.")]
        [Display(Name = "Product Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public StockCategory Category { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, 9999.99, ErrorMessage = "Price must be between $0.01 and $9999.99.")]
        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(10, 2)")]
        [Display(Name = "Price")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0, 10000, ErrorMessage = "Quantity must be 0 or greater.")]
        [Display(Name = "Quantity in Stock")]
        public int Quantity { get; set; }

        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [Display(Name = "Last Updated")]
        public DateTime? LastUpdated { get; set; }

        [Display(Name = "Units Sold")]
        public int UnitsSold { get; set; } = 0;

        [Range(0, 5, ErrorMessage = "Rating must be between 0 and 5.")]
        [Display(Name = "Rating")]
        public double Rating { get; set; } = 0;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Featured Item")]
        public bool IsFeatured { get; set; } = false;

        // Navigation properties
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

        // Helper methods
        public bool IsInStock() => Quantity > 0;

        public decimal GetDiscountedPrice(decimal discountRate)
        {
            return Price * (1 - discountRate);
        }

        public void UpdateStock(int quantitySold)
        {
            if (quantitySold <= Quantity)
            {
                Quantity -= quantitySold;
                UnitsSold += quantitySold;
                LastUpdated = DateTime.Now;
            }
        }

        public decimal GetTotalRevenue()
        {
            return Price * UnitsSold;
        }
    }
}
// Claude Prompt 016 end