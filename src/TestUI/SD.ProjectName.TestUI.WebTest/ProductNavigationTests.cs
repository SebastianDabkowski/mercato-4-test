using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Infrastructure;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class ProductNavigationTests : PageTest, IClassFixture<WebAppFixture>
    {
        private readonly WebAppFixture _fixture;

        public ProductNavigationTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ProductDetailPage_ShowsCategoryLink()
        {
            await SeedProductWithCategoryAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/1", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            await Expect(Page.GetByTestId("category-link")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("category-link")).ToContainTextAsync("Books");

            await Page.GetByTestId("category-link").ClickAsync();
            await Page.WaitForURLAsync(new Regex("category=Books", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("category-heading")).ToContainTextAsync("Books");
        }

        [Fact]
        public async Task ProductDetailPage_ShowsSellerLink_WhenSellerHasStoreName()
        {
            await SeedProductWithSellerAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/1", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            await Expect(Page.GetByTestId("product-seller")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("seller-link")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("seller-link")).ToContainTextAsync("Test Store");

            await Page.GetByTestId("seller-link").ClickAsync();
            await Page.WaitForURLAsync(new Regex("/store/test-store", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("public-store-name")).ToContainTextAsync("Test Store");
        }

        [Fact]
        public async Task ProductDetailPage_ShowsBackToResultsLink_WhenComingFromCategoryPage()
        {
            await SeedProductWithCategoryAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/list?category=Books", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Page.GetByTestId("product-detail-link").First.ClickAsync();
            await Page.WaitForURLAsync(new Regex("/products/1", RegexOptions.IgnoreCase));

            await Expect(Page.GetByTestId("back-to-results")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("back-to-results")).ToContainTextAsync("Back to results");

            await Page.GetByTestId("back-to-results").ClickAsync();
            await Page.WaitForURLAsync(new Regex("category=Books", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("category-heading")).ToContainTextAsync("Books");
        }

        [Fact]
        public async Task ProductDetailPage_ShowsBackToResultsLink_WhenComingFromSearchPage()
        {
            await SeedProductWithCategoryAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/search?q=Test", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Page.GetByTestId("product-detail-link").First.ClickAsync();
            await Page.WaitForURLAsync(new Regex("/products/1", RegexOptions.IgnoreCase));

            await Expect(Page.GetByTestId("back-to-results")).ToBeVisibleAsync();

            await Page.GetByTestId("back-to-results").ClickAsync();
            await Page.WaitForURLAsync(new Regex("q=Test", RegexOptions.IgnoreCase));
            await Expect(Page).ToHaveURLAsync(new Regex("/search", RegexOptions.IgnoreCase));
        }

        [Fact]
        public async Task ProductDetailPage_DoesNotShowBackToResultsLink_WhenComingDirectly()
        {
            await SeedProductWithCategoryAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/1", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            await Expect(Page.GetByTestId("back-to-results")).Not.ToBeVisibleAsync();
        }

        [Fact]
        public async Task ProductDetailPage_BackToResults_PreservesFiltersAndSortOrder()
        {
            await SeedMultipleProductsAsync();

            var categoryPageUrl = $"{_fixture.BaseUrl}/products/list?category=Books&sort=PriceAsc&minPrice=10&maxPrice=50";
            await Page.GotoAsync(categoryPageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await Page.GetByTestId("product-detail-link").First.ClickAsync();
            await Page.WaitForURLAsync(new Regex("/products/", RegexOptions.IgnoreCase));

            await Expect(Page.GetByTestId("back-to-results")).ToBeVisibleAsync();

            await Page.GetByTestId("back-to-results").ClickAsync();
            await Page.WaitForURLAsync(new Regex("category=Books", RegexOptions.IgnoreCase));

            await Expect(Page).ToHaveURLAsync(new Regex("sort=PriceAsc", RegexOptions.IgnoreCase));
            await Expect(Page).ToHaveURLAsync(new Regex("minPrice=10", RegexOptions.IgnoreCase));
            await Expect(Page).ToHaveURLAsync(new Regex("maxPrice=50", RegexOptions.IgnoreCase));
        }

        private async Task SeedProductWithCategoryAsync()
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseSqlite(_fixture.ConnectionString)
                .Options;

            using var db = new ProductDbContext(options);
            await db.Database.EnsureCreatedAsync();

            db.Products.RemoveRange(db.Products);
            db.Categories.RemoveRange(db.Categories);
            await db.SaveChangesAsync();

            var books = new CategoryModel { Name = "Books", NormalizedName = "BOOKS", DisplayOrder = 0, IsActive = true };
            db.Categories.Add(books);
            await db.SaveChangesAsync();

            var product = new ProductModel
            {
                Name = "Test Book",
                Category = "Books",
                Price = 29.99m,
                Stock = 5,
                Description = "Test description",
                SellerId = "test-seller-id",
                Status = ProductStatuses.Active
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        private async Task SeedProductWithSellerAsync()
        {
            var productsOptions = new DbContextOptionsBuilder<ProductDbContext>()
                .UseSqlite(_fixture.ConnectionString)
                .Options;

            using var productsDb = new ProductDbContext(productsOptions);
            await productsDb.Database.EnsureCreatedAsync();

            productsDb.Products.RemoveRange(productsDb.Products);
            productsDb.Categories.RemoveRange(productsDb.Categories);
            await productsDb.SaveChangesAsync();

            var books = new CategoryModel { Name = "Books", NormalizedName = "BOOKS", DisplayOrder = 0, IsActive = true };
            productsDb.Categories.Add(books);
            await productsDb.SaveChangesAsync();

            // Seed seller in Identity database
            var identityOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_fixture.ConnectionString)
                .Options;

            using var identityDb = new ApplicationDbContext(identityOptions);
            await identityDb.Database.EnsureCreatedAsync();

            // Remove existing test seller if present
            var existing = identityDb.Users.FirstOrDefault(u => u.Id == "test-seller-id");
            if (existing != null)
            {
                identityDb.Users.Remove(existing);
                await identityDb.SaveChangesAsync();
            }

            var seller = new ApplicationUser
            {
                Id = "test-seller-id",
                UserName = "testseller@example.com",
                Email = "testseller@example.com",
                NormalizedUserName = "TESTSELLER@EXAMPLE.COM",
                NormalizedEmail = "TESTSELLER@EXAMPLE.COM",
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "Seller",
                AccountType = AccountType.Seller,
                AccountStatus = AccountStatus.Verified,
                StoreName = "Test Store",
                StoreContactEmail = "store@example.com",
                SecurityStamp = Guid.NewGuid().ToString()
            };

            identityDb.Users.Add(seller);
            await identityDb.SaveChangesAsync();

            var product = new ProductModel
            {
                Name = "Test Book",
                Category = "Books",
                Price = 29.99m,
                Stock = 5,
                Description = "Test description",
                SellerId = "test-seller-id",
                Status = ProductStatuses.Active
            };

            productsDb.Products.Add(product);
            await productsDb.SaveChangesAsync();
        }

        private async Task SeedMultipleProductsAsync()
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseSqlite(_fixture.ConnectionString)
                .Options;

            using var db = new ProductDbContext(options);
            await db.Database.EnsureCreatedAsync();

            db.Products.RemoveRange(db.Products);
            db.Categories.RemoveRange(db.Categories);
            await db.SaveChangesAsync();

            var books = new CategoryModel { Name = "Books", NormalizedName = "BOOKS", DisplayOrder = 0, IsActive = true };
            db.Categories.Add(books);
            await db.SaveChangesAsync();

            for (var i = 1; i <= 5; i++)
            {
                var product = new ProductModel
                {
                    Name = $"Book {i}",
                    Category = "Books",
                    Price = 10 + (i * 10),
                    Stock = 5,
                    Description = $"Description {i}",
                    SellerId = "test-seller-id",
                    Status = ProductStatuses.Active
                };

                db.Products.Add(product);
            }

            await db.SaveChangesAsync();
        }
    }
}
