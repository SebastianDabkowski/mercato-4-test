using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.WebApp.Data;
using Xunit;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class ProductImportTests : PageTest, IClassFixture<WebAppFixture>
    {
        private const string Password = "S3cure!Passw0rd";
        private readonly WebAppFixture _fixture;

        public ProductImportTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SellerCanPreviewAndImportCatalog()
        {
            var email = await SeedSellerAsync();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));

            await Page.GotoAsync($"{_fixture.BaseUrl}/seller/products/import");

            var csv = """
                      Sku,Title,Price,Stock,Category,Description
                      IMP-001,Imported Product 1,19.99,5,Books,First imported item
                      IMP-002,Imported Product 2,9.50,2,Gadgets,Second imported item
                      """;
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, csv);

            await Page.GetByTestId("import-upload").SetInputFilesAsync(tempFile);
            await Page.GetByTestId("import-preview-button").ClickAsync();
            await Expect(Page.GetByTestId("import-preview-summary")).ToContainTextAsync("Total rows: 2");
            await Page.GetByTestId("import-confirm-button").ClickAsync();

            await Expect(Page.GetByTestId("import-status")).ToContainTextAsync("Import finished", new() { Timeout = 15000 });

            await Page.GotoAsync($"{_fixture.BaseUrl}/seller/products");
            await Expect(Page.GetByTestId("seller-products-table")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("seller-product-row")).ToContainTextAsync("Imported Product 1");
            await Expect(Page.GetByTestId("seller-product-row")).ToContainTextAsync("Imported Product 2");
        }

        private async Task<string> SeedSellerAsync()
        {
            var email = $"seller-{Guid.NewGuid():N}@example.com";
            var request = new
            {
                AccountType = AccountType.Seller,
                Email = email,
                Password = Password,
                EmailConfirmed = true,
                EnableTwoFactor = false
            };

            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync($"{_fixture.BaseUrl}/_test/create-account", request);
            response.EnsureSuccessStatusCode();
            return email;
        }
    }
}
