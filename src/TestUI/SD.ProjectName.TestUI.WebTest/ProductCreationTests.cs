using System;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.WebApp.Data;
using Xunit;

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

        [Fact]
        public async Task SellerCanEditAndPublishProduct_ShowsUpdatedDetails()
        {
            var email = await SeedSellerAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));

            await Page.GotoAsync($"{_fixture.BaseUrl}/seller/products/create");
            await Page.GetByTestId("product-title").FillAsync("Draft Product");
            await Page.GetByTestId("product-category").FillAsync("Gadgets");
            await Page.GetByTestId("product-price").FillAsync("9.99");
            await Page.GetByTestId("product-stock").FillAsync("4");
            await Page.GetByTestId("product-description").FillAsync("A draft product.");
            await Page.GetByTestId("save-product").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/products", RegexOptions.IgnoreCase));
            await Page.GetByTestId("edit-product-link").First.ClickAsync();

            await Page.GetByTestId("product-title").FillAsync("Updated Product");
            await Page.GetByTestId("product-category").FillAsync("Updated Category");
            await Page.GetByTestId("product-price").FillAsync("29.99");
            await Page.GetByTestId("product-stock").FillAsync("8");
            await Page.GetByTestId("product-description").FillAsync("Updated description for buyers.");
            await Page.GetByTestId("product-images").FillAsync("https://example.com/image1.jpg\nhttps://example.com/image2.jpg");
            await Page.GetByTestId("product-weight").FillAsync("2.5");
            await Page.GetByTestId("product-length").FillAsync("10");
            await Page.GetByTestId("product-width").FillAsync("5");
            await Page.GetByTestId("product-height").FillAsync("2");
            await Page.GetByTestId("product-shipping").FillAsync("Courier Express\nParcel Locker");
            await Page.GetByTestId("product-publish").CheckAsync();
            await Page.GetByTestId("save-product").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/products", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("products-status")).ToContainTextAsync("Product updated");
            await Expect(Page.GetByTestId("product-status").First).ToContainTextAsync("active", new() { Timeout = 15000 });

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/list?category=Updated%20Category");
            await Expect(Page.GetByTestId("product-row").First).ToContainTextAsync("Updated Product");
            await Expect(Page.GetByTestId("product-row").First).ToContainTextAsync("29.99");
            await Page.GetByTestId("product-detail-link").First.ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/products/\\d+", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("product-title")).ToContainTextAsync("Updated Product");
            await Expect(Page.GetByTestId("product-description")).ToContainTextAsync("Updated description");
            await Expect(Page.GetByTestId("product-category")).ToContainTextAsync("Updated Category");
            await Expect(Page.GetByTestId("product-stock")).ToContainTextAsync("8");
            await Expect(Page.GetByTestId("product-price")).ToContainTextAsync("29.99");
            await Expect(Page.GetByTestId("product-weight")).ToContainTextAsync("2.50");
            await Expect(Page.GetByTestId("product-dimensions")).ToContainTextAsync("10.0");
            await Expect(Page.GetByTestId("product-shipping-methods")).ToContainTextAsync("Courier Express");
            var imageCount = await Page.GetByTestId("product-images").Locator("img").CountAsync();
            Assert.True(imageCount >= 2);
        }

        [Fact]
        public async Task SellerCanDeleteProduct_RemovesFromListingsAndDetails()
        {
            var email = await SeedSellerAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));

            await Page.GotoAsync($"{_fixture.BaseUrl}/seller/products/create");
            await Page.GetByTestId("product-title").FillAsync("Delete Me");
            await Page.GetByTestId("product-category").FillAsync("Archive");
            await Page.GetByTestId("product-price").FillAsync("5.00");
            await Page.GetByTestId("product-stock").FillAsync("2");
            await Page.GetByTestId("save-product").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/products", RegexOptions.IgnoreCase));
            var editHref = await Page.GetByTestId("edit-product-link").First.GetAttributeAsync("href");
            await Page.GetByTestId("delete-product-button").First.ClickAsync();

            await Expect(Page.GetByTestId("products-status")).ToContainTextAsync("archived");
            await Expect(Page.GetByTestId("seller-products-table")).NotToContainTextAsync("Delete Me");

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/list?category=Archive");
            await Expect(Page.Locator("[data-testid='product-row']")).ToHaveCountAsync(0);

            var productId = editHref?.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            await Page.GotoAsync($"{_fixture.BaseUrl}/products/{productId}");
            await Expect(Page.GetByTestId("product-not-found")).ToContainTextAsync("unavailable");
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
