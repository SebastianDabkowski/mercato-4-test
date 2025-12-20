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

        private async Task SeedActiveProductAsync(string name, string description, string category, decimal price)
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
                Status = ProductStatuses.Active,
                Sku = $"SKU-{Guid.NewGuid():N}"
            });
            await context.SaveChangesAsync();
        }
    }
}
