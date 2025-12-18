using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class ProductCreationTests : PageTest, IClassFixture<WebAppFixture>
    {
        private const string Password = "S3cure!Passw0rd";
        private readonly WebAppFixture _fixture;

        public ProductCreationTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SellerCanCreateProduct_ShowsDraftAndValidations()
        {
            var email = await SeedSellerAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));

            await Page.GotoAsync($"{_fixture.BaseUrl}/seller/products/create");

            await Page.GetByTestId("save-product").ClickAsync();
            await Expect(Page.GetByTestId("validation-summary")).ToContainTextAsync("required");

            await Page.GetByTestId("product-title").FillAsync("Test Product");
            await Page.GetByTestId("product-category").FillAsync("Books");
            await Page.GetByTestId("product-price").FillAsync("19.99");
            await Page.GetByTestId("product-stock").FillAsync("5");
            await Page.GetByTestId("product-description").FillAsync("A draft product.");
            await Page.GetByTestId("save-product").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/products", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("products-status")).ToContainTextAsync("draft");
            await Expect(Page.GetByTestId("seller-products-table")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("seller-product-row")).ToContainTextAsync("Test Product");
            await Expect(Page.GetByTestId("product-status").First).ToContainTextAsync("draft", new() { Timeout = 15000 });
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
