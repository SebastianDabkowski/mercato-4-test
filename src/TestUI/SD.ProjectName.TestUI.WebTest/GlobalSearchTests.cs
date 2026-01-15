using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class GlobalSearchTests : PageTest, IClassFixture<WebAppFixture>
    {
        private readonly WebAppFixture _fixture;

        public GlobalSearchTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GlobalSearch_ShowsMatchingActiveProducts()
        {
            var productName = $"Searchable Product {Guid.NewGuid():N}";
            await SeedActiveProductAsync(productName, "Searchable description for buyers", "SearchCat", 19.99m);

            await Page.GotoAsync($"{_fixture.BaseUrl}/");
            await Page.GetByTestId("global-search-input").FillAsync("Searchable");
            await Page.GetByTestId("global-search-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/search", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("sort-select")).ToHaveValueAsync("Relevance");
            await Expect(Page.GetByTestId("product-row")).ToContainTextAsync(productName);
            await Expect(Page.GetByTestId("product-row")).ToContainTextAsync("SearchCat");
        }

        [Fact]
        public async Task GlobalSearch_ShowsMessage_WhenNoMatches()
        {
            await Page.GotoAsync($"{_fixture.BaseUrl}/");
            await Page.GetByTestId("global-search-input").FillAsync($"no-matches-{Guid.NewGuid():N}");
            await Page.GetByTestId("global-search-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/search", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("no-results-message")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task GlobalSearch_AllowsFilteringByConditionSellerAndPrice()
        {
            var keyword = $"Filterable {Guid.NewGuid():N}";
            await SeedActiveProductAsync($"{keyword} New", "Filter target", "Filters", 25m, ProductConditions.New, "seller-alpha");
            await SeedActiveProductAsync($"{keyword} Used", "Filter target", "Filters", 45m, ProductConditions.Used, "seller-beta");

            await Page.GotoAsync($"{_fixture.BaseUrl}/search?q={Uri.EscapeDataString(keyword)}");
            await Page.GetByTestId("filter-panel-toggle").ClickAsync();
            await Expect(Page.GetByTestId("filter-category")).ToBeVisibleAsync();
            await Page.GetByTestId("filter-condition").SelectOptionAsync(ProductConditions.Used);
            await Page.GetByTestId("filter-seller").SelectOptionAsync("seller-beta");
            await Page.GetByTestId("filter-max-price").FillAsync("50");
            await Page.GetByTestId("apply-filters").ClickAsync();

            await Expect(Page.GetByTestId("product-row")).ToHaveCountAsync(1);
            await Expect(Page.GetByTestId("product-row")).ToContainTextAsync($"{keyword} Used");
        }

        private async Task SeedActiveProductAsync(
            string name,
            string description,
            string category,
            decimal price,
            string? condition = null,
            string? sellerId = null)
        {
            var options = new DbContextOptionsBuilder<ProductDbContext>()
                .UseSqlite(_fixture.ConnectionString)
                .Options;

            await using var context = new ProductDbContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Products.Add(new ProductModel
            {
                Name = name,
                Description = description,
                Category = category,
                Price = price,
                Stock = 10,
                Condition = condition ?? ProductConditions.New,
                Status = ProductStatuses.Active,
                Sku = $"SKU-{Guid.NewGuid():N}",
                SellerId = sellerId ?? $"seller-{Guid.NewGuid():N}"
            });
            await context.SaveChangesAsync();
        }
    }
}
