using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.WebApp.Data;
using Xunit;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class CategoryManagementTests : PageTest, IClassFixture<WebAppFixture>
    {
        private const string Password = "S3cure!Passw0rd";
        private readonly WebAppFixture _fixture;

        public CategoryManagementTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task AdminCanManageCategoriesAndDeletionBlockedWhenInUse()
        {
            var adminEmail = await SeedAccountAsync(AccountType.Seller, isAdmin: true);

            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(adminEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/admin/dashboard", RegexOptions.IgnoreCase));
            await Page.GotoAsync($"{_fixture.BaseUrl}/admin/categories");
            await Expect(Page.GetByTestId("categories-heading")).ToBeVisibleAsync();

            await Page.GetByTestId("category-name-input").FillAsync("Electronics");
            await Page.GetByTestId("create-category-button").ClickAsync();
            await Expect(Page.GetByTestId("status-message")).ToContainTextAsync("created");

            await Page.GetByTestId("category-name-input").FillAsync("Phones");
            await Page.GetByTestId("category-parent-select").SelectOptionAsync(new SelectOptionValue { Label = "Electronics" }, new() { Timeout = 10000 });
            await Page.GetByTestId("create-category-button").ClickAsync();
            await Expect(Page.GetByTestId("category-tree")).ToContainTextAsync("Phones");

            var phonesNode = Page.GetByTestId("category-node").Filter(new() { HasText = "Phones" });
            await phonesNode.GetByTestId("rename-input").FillAsync("Smartphones");
            await phonesNode.GetByTestId("rename-button").ClickAsync();
            await Expect(Page.GetByTestId("category-tree")).ToContainTextAsync("Smartphones");

            var sellerEmail = await SeedAccountAsync(AccountType.Seller);
            await using var sellerContext = await Browser.NewContextAsync();
            var sellerPage = await sellerContext.NewPageAsync();
            await sellerPage.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await sellerPage.GetByTestId("login-email").FillAsync(sellerEmail);
            await sellerPage.GetByTestId("login-password").FillAsync(Password);
            await sellerPage.GetByTestId("login-submit").ClickAsync();
            await Expect(sellerPage).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));

            await sellerPage.GotoAsync($"{_fixture.BaseUrl}/seller/products/create");
            await sellerPage.GetByTestId("product-title").FillAsync("Phone Model X");
            await sellerPage.GetByTestId("product-category").SelectOptionAsync(new[] { "Smartphones" });
            await sellerPage.GetByTestId("product-price").FillAsync("199.99");
            await sellerPage.GetByTestId("product-stock").FillAsync("3");
            await sellerPage.GetByTestId("product-description").FillAsync("Latest smartphone.");
            await sellerPage.GetByTestId("save-product").ClickAsync();
            await Expect(sellerPage).ToHaveURLAsync(new Regex("/seller/products", RegexOptions.IgnoreCase));

            await sellerContext.CloseAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/admin/categories");
            var smartphoneNode = Page.GetByTestId("category-node").Filter(new() { HasText = "Smartphones" });
            await smartphoneNode.GetByTestId("delete-category").ClickAsync();
            await Expect(Page.GetByTestId("error-message")).ToContainTextAsync("assigned");
        }

        private async Task<string> SeedAccountAsync(AccountType accountType, bool isAdmin = false)
        {
            var email = $"{(isAdmin ? "admin" : "user")}-{Guid.NewGuid():N}@example.com";
            var request = new
            {
                AccountType = accountType,
                Email = email,
                Password = Password,
                EmailConfirmed = true,
                EnableTwoFactor = false,
                IsAdmin = isAdmin
            };

            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync($"{_fixture.BaseUrl}/_test/create-account", request);
            response.EnsureSuccessStatusCode();
            return email;
        }
    }
}
