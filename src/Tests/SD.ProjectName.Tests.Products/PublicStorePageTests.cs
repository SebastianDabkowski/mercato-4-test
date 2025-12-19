using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Pages.Stores;
using SD.ProjectName.WebApp.Stores;

namespace SD.ProjectName.Tests.Products;

public class PublicStorePageTests
{
    [Fact]
    public async Task OnGet_ShouldReturnNotFound_WhenStoreSuspended()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;
        var dbContext = scoped.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();

        var userManager = scoped.GetRequiredService<UserManager<ApplicationUser>>();
        var storeOwner = new ApplicationUser
        {
            Id = "seller-1",
            UserName = "seller@example.com",
            Email = "seller@example.com",
            FirstName = "Test",
            LastName = "Seller",
            AccountType = AccountType.Seller,
            AccountStatus = AccountStatus.Suspended,
            StoreName = "Suspended Store",
            TermsAcceptedAt = DateTimeOffset.UtcNow
        };

        await userManager.CreateAsync(storeOwner);

        var getProducts = new GetProducts(new FakeProductRepository([]));
        var page = new ProfileModel(userManager, getProducts);

        var result = await page.OnGetAsync(StoreUrlHelper.ToSlug(storeOwner.StoreName!));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGet_ShouldLoadStoreProfileAndProducts_WhenVerified()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;
        var dbContext = scoped.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();

        var userManager = scoped.GetRequiredService<UserManager<ApplicationUser>>();
        var storeOwner = new ApplicationUser
        {
            Id = "seller-verified",
            UserName = "verified@example.com",
            Email = "verified@example.com",
            FirstName = "Demo",
            LastName = "Seller",
            AccountType = AccountType.Seller,
            AccountStatus = AccountStatus.Verified,
            StoreName = "Demo Store",
            StoreDescription = "Great products here.",
            StoreContactEmail = "contact@demo-store.test",
            StoreContactPhone = "+1-555-0100",
            StoreWebsiteUrl = "https://demo-store.test",
            TermsAcceptedAt = DateTimeOffset.UtcNow
        };

        await userManager.CreateAsync(storeOwner);

        var products = new List<ProductModel>
        {
            new() { Id = 1, Name = "Product 1", Description = "First product", Price = 10m, Status = ProductStatuses.Active, Category = "General", Stock = 5, SellerId = storeOwner.Id },
            new() { Id = 2, Name = "Product 2", Description = "Second product", Price = 20m, Status = ProductStatuses.Active, Category = "General", Stock = 3, SellerId = storeOwner.Id }
        };

        var getProducts = new GetProducts(new FakeProductRepository(products));
        var page = new ProfileModel(userManager, getProducts);

        var result = await page.OnGetAsync(StoreUrlHelper.ToSlug(storeOwner.StoreName!));

        Assert.IsType<PageResult>(result);
        Assert.Equal(storeOwner.StoreName, page.StoreName);
        Assert.Equal(storeOwner.StoreDescription, page.StoreDescription);
        Assert.Equal(storeOwner.StoreContactEmail, page.ContactEmail);
        Assert.Equal(products.Count, page.ProductPreviews.Count);
        Assert.Equal(products[0].Name, page.ProductPreviews[0].Name);
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        return services.BuildServiceProvider();
    }

    private class FakeProductRepository : IProductRepository
    {
        private readonly List<ProductModel> _products;

        public FakeProductRepository(List<ProductModel> products)
        {
            _products = products;
        }

        public Task<List<ProductModel>> GetList(string? category = null)
        {
            var query = _products.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category == category);
            }

            return Task.FromResult(query.ToList());
        }

        public Task<List<ProductModel>> GetBySeller(string sellerId, bool includeDrafts)
        {
            var items = includeDrafts
                ? _products
                : _products.Where(p => p.Status == ProductStatuses.Active).ToList();
            return Task.FromResult(items);
        }

        public Task<ProductModel?> GetById(int id)
        {
            return Task.FromResult(_products.FirstOrDefault(p => p.Id == id));
        }

        public Task Update(ProductModel product)
        {
            var existing = _products.FirstOrDefault(p => p.Id == product.Id);
            if (existing is not null)
            {
                _products.Remove(existing);
            }
            _products.Add(product);
            return Task.CompletedTask;
        }

        public Task<ProductModel> Add(ProductModel product)
        {
            _products.Add(product);
            return Task.FromResult(product);
        }

        public Task<bool> AnyWithCategory(string categoryName)
        {
            var normalized = categoryName.Trim();
            return Task.FromResult(_products.Any(p => string.Equals(p.Category, normalized, StringComparison.Ordinal)));
        }

        public Task<int> UpdateCategoryName(string oldCategoryName, string newCategoryName)
        {
            var normalizedOld = oldCategoryName.Trim();
            var updated = 0;
            foreach (var product in _products.Where(p => string.Equals(p.Category, normalizedOld, StringComparison.Ordinal)))
            {
                product.Category = newCategoryName.Trim();
                updated++;
            }

            return Task.FromResult(updated);
        }
    }
}
