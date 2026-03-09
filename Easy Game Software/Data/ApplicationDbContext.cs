using Easy_Games_Software.Models;
using Microsoft.EntityFrameworkCore;

// Claude Prompt 001 start
namespace Easy_Games_Software.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<StockItem> StockItems { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<CartItem> CartItems { get; set; } = null!;
        public DbSet<Shop> Shops { get; set; } = null!;
        public DbSet<ShopStock> ShopStocks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.TotalSpent).HasPrecision(10, 2);
            });

            // Configure StockItem entity
            modelBuilder.Entity<StockItem>(entity =>
            {
                entity.Property(e => e.Price).HasPrecision(10, 2);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsActive);
            });

            // Configure Transaction entity
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
                entity.Property(e => e.DiscountAmount).HasPrecision(10, 2);
                entity.Property(e => e.TotalAmount).HasPrecision(10, 2);

                entity.HasOne(t => t.User)
                    .WithMany(u => u.Transactions)
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.StockItem)
                    .WithMany(s => s.Transactions)
                    .HasForeignKey(t => t.StockItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Shop)
                    .WithMany(s => s.Transactions)
                    .HasForeignKey(t => t.ShopId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure CartItem entity
            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasOne(c => c.User)
                    .WithMany(u => u.CartItems)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.StockItem)
                    .WithMany(s => s.CartItems)
                    .HasForeignKey(c => c.StockItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.StockItemId }).IsUnique();
            });

            //Configure Shop entity
            modelBuilder.Entity<Shop>(entity =>
            {
                entity.HasOne(s => s.ShopProprietor)
                    .WithMany()
                    .HasForeignKey(s => s.ShopProprietorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.ShopName);
                entity.HasIndex(e => e.IsActive);
            });

            // Configure ShopStock entity
            modelBuilder.Entity<ShopStock>(entity =>
            {
                entity.HasOne(ss => ss.Shop)
                    .WithMany(s => s.ShopStocks)
                    .HasForeignKey(ss => ss.ShopId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ss => ss.StockItem)
                    .WithMany()
                    .HasForeignKey(ss => ss.StockItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Ensure unique stock items per shop
                entity.HasIndex(e => new { e.ShopId, e.StockItemId }).IsUnique();
            });
        }
    }
}
// Claude Prompt 001 end