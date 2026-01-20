using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class ProductCategoryBrowsingTests : PageTest, IClassFixture<WebAppFixture>
    {
        private readonly WebAppFixture _fixture;

        public ProductCategoryBrowsingTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task BuyerCanBrowseCategoriesAndSeeEmptyStates()
        {
            await SeedCatalogAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/list?category=Books", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            await Expect(Page.GetByTestId("category-heading")).ToContainTextAsync("Books");
            await Expect(Page.GetByTestId("sort-select")).ToHaveValueAsync("Newest");
            await Expect(Page.GetByTestId("product-row")).ToHaveCountAsync(1);
            await Expect(Page.GetByTestId("product-row")).ToContainTextAsync("Dune");
            await Expect(Page.GetByTestId("subcategory-link").Filter(new() { HasText = "SciFi" })).ToBeVisibleAsync();

            await Page.GetByTestId("subcategory-link").Filter(new() { HasText = "SciFi" }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("category=SciFi", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("product-row")).ToContainTextAsync("Dune");

            await Page.GetByTestId("category-link").Filter(new() { HasText = "Home" }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("category=Home", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("empty-category-state")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("category-suggestion")).ToContainTextAsync("Books");
        }

        [Fact]
        public async Task BuyerCanUsePaginationInCategoryListing()
        {
            await SeedCatalogWithManyProductsAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/list?category=Books", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            await Expect(Page.GetByTestId("pagination")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("pagination-page-2")).ToBeVisibleAsync();
        }

        private async Task SeedCatalogAsync()
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
            var sciFi = new CategoryModel { Name = "SciFi", NormalizedName = "SCIFI", DisplayOrder = 1, IsActive = true, Parent = books };
            var home = new CategoryModel { Name = "Home", NormalizedName = "HOME", DisplayOrder = 2, IsActive = true };

            db.Categories.AddRange(books, sciFi, home);
            await db.SaveChangesAsync();

            var product = new ProductModel
            {
                Name = "Dune",
                Category = "SciFi",
                Price = 29.99m,
                Stock = 5,
                Description = "Classic sci-fi novel",
                ImageUrls = "http://example.com/dune.jpg",
                SellerId = "seeded-seller",
                Status = ProductStatuses.Active
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        private async Task SeedCatalogWithManyProductsAsync()
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

            var products = new List<ProductModel>();
            for (var i = 0; i < 15; i++)
            {
                products.Add(new ProductModel
                {
                    Name = $"Book {i}",
                    Category = "Books",
                    Price = 10 + i,
                    Stock = 5,
                    Description = $"Book {i} description",
                    SellerId = $"seller-{i}",
                    Status = ProductStatuses.Active
                });
            }

            db.Products.AddRange(products);
            await db.SaveChangesAsync();
        }
    }
}
