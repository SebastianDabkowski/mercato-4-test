using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class CategoryFilterTests : PageTest, IClassFixture<WebAppFixture>
    {
        private readonly WebAppFixture _fixture;

        public CategoryFilterTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CategoryList_ShowsEmptyState_WhenFiltersExcludeAll()
        {
            var category = $"Accessories-{Guid.NewGuid():N}";
            await SeedCategoryAsync(category);
            await SeedActiveProductAsync("Accessory", category, 25m, ProductConditions.New, "seller-one");

            await Page.GotoAsync($"{_fixture.BaseUrl}/Products/List?category={Uri.EscapeDataString(category)}");
            await Page.GetByTestId("filter-panel-toggle").ClickAsync();
            await Page.GetByTestId("filter-min-price").FillAsync("1000");
            await Page.GetByTestId("apply-filters").ClickAsync();

            await Expect(Page.GetByTestId("empty-category-state")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("empty-active-filters")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("reset-filters")).ToBeVisibleAsync();
        }

        private async Task SeedCategoryAsync(string name)
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseSqlite(_fixture.ConnectionString)
                .Options;

            await using var context = new ProductDbContext(options);
            await context.Database.EnsureCreatedAsync();
            if (!context.Categories.Any(c => c.Name == name))
            {
                context.Categories.Add(new CategoryModel
                {
                    Name = name,
                    NormalizedName = name.ToUpperInvariant(),
                    DisplayOrder = 0,
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }
        }

        private async Task SeedActiveProductAsync(string name, string category, decimal price, string condition, string sellerId)
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseSqlite(_fixture.ConnectionString)
                .Options;

            await using var context = new ProductDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Products.Add(new ProductModel
            {
                Name = name,
                Description = "Category filter product",
                Category = category,
                Price = price,
                Stock = 5,
                Condition = condition,
                Status = ProductStatuses.Active,
                SellerId = sellerId,
                Sku = $"SKU-{Guid.NewGuid():N}"
            });
            await context.SaveChangesAsync();
        }
    }
}
