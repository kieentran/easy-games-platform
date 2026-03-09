using Easy_Games_Software.Data;
using Easy_Games_Software.Models;
using Easy_Games_Software.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

// Claude Prompt 001, 001.5 start - Application Setup and Configuration
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Configure Entity Framework with SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=easygames.db"));

// Add Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
    });

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register custom services with Dependency Injection
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IRewardService, RewardService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IShopService, ShopService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Add HttpContextAccessor for accessing session in services
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Seed database with default data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Ensure database is created
    await context.Database.EnsureCreatedAsync();

    // Seed default admin if no users exist
    if (!await context.Users.AnyAsync())
    {
        // Helper method to hash password
        static string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        // Seed default owner account
        var owner = new User
        {
            Username = "admin",
            Password = HashPassword("admin123"),
            Role = UserRole.Owner,
            Email = "admin@easygames.com",
            FullName = "System Administrator",
            Points = 0,
            Tier = UserTier.Silver,
            RegistrationDate = DateTime.Now,
            IsActive = true
        };
        context.Users.Add(owner);

        // Seed sample stock items
        var sampleItems = new List<StockItem>
        {
            new StockItem
            {
                Name = "The Legend of Zelda: Tears of the Kingdom",
                Description = "Epic adventure game for Nintendo Switch",
                Category = StockCategory.Game,
                Price = 69.99m,
                Quantity = 50,
                Rating = 4.8,
                IsFeatured = true
            },
            new StockItem
            {
                Name = "Black Myth: Wukong",
                Description = "Epic Chinese mythology action RPG",
                Category = StockCategory.RPG,
                Price = 69.99m,
                Quantity = 30,
                Rating = 4.9,
                IsFeatured = true
            },
            new StockItem
            {
                Name = "The Last of Us",
                Description = "Critically acclaimed post-apocalyptic action-adventure",
                Category = StockCategory.Action,
                Price = 69.99m,
                Quantity = 40,
                Rating = 4.9
            },
            new StockItem
            {
                Name = "FIFA 25",
                Description = "Latest football simulation game",
                Category = StockCategory.Sports,
                Price = 59.99m,
                Quantity = 60,
                Rating = 4.2
            },
            new StockItem
            {
                Name = "Portal Companion Cube Plush",
                Description = "Soft toy from the Portal game series",
                Category = StockCategory.Toy,
                Price = 24.99m,
                Quantity = 100,
                Rating = 4.9
            },
            new StockItem
            {
                Name = "The Art of Game Design",
                Description = "Essential reading for game developers",
                Category = StockCategory.Book,
                Price = 34.99m,
                Quantity = 20,
                Rating = 4.7
            }
        };

        context.StockItems.AddRange(sampleItems);
        await context.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Store}/{action=Index}/{id?}");

app.Run();
// Claude Prompt 001, 001.5 end