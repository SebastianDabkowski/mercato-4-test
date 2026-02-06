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
    public class CartTests : PageTest, IClassFixture<WebAppFixture>
    {
        private const string Password = "S3cure!Passw0rd";
        private readonly WebAppFixture _fixture;

        public CartTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task BuyerCanAddProductToCart_RedirectsToCartPage()
        {
            // Arrange: Create buyer and seller with product
            var buyerEmail = await SeedBuyerAsync();
            var sellerEmail = await SeedSellerAsync();
            var productId = await CreateAndPublishProductAsync(sellerEmail, "Test Product", "Electronics", "99.99", "10");

            // Act: Login as buyer
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(buyerEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));

            // Navigate to product details and add to cart
            await Page.GotoAsync($"{_fixture.BaseUrl}/products/{productId}");
            await Expect(Page.GetByTestId("add-to-cart-btn")).ToBeVisibleAsync();
            await Page.GetByTestId("add-to-cart-btn").ClickAsync();

            // Assert: Redirected to cart page
            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/cart", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("cart-heading")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("cart-item-name")).ToContainTextAsync("Test Product");
            await Expect(Page.GetByTestId("cart-item-price")).ToContainTextAsync("99.99");
        }

        [Fact]
        public async Task BuyerCanSeeCartLinkInNavbar_WhenAuthenticated()
        {
            // Arrange
            var buyerEmail = await SeedBuyerAsync();

            // Act: Login as buyer
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(buyerEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));

            // Assert: Cart link is visible
            await Expect(Page.GetByTestId("cart-link")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task CartPage_ShowsEmptyState_WhenNoItems()
        {
            // Arrange
            var buyerEmail = await SeedBuyerAsync();

            // Act: Login as buyer and navigate to cart
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(buyerEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/buyer/cart");

            // Assert: Empty state is shown
            await Expect(Page.GetByTestId("cart-empty")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("cart-empty")).ToContainTextAsync("Your cart is empty");
        }

        [Fact]
        public async Task CartPage_GroupsItemsBySeller_ShowsSellerNames()
        {
            // Arrange: Create buyer and two sellers with products
            var buyerEmail = await SeedBuyerAsync();
            var seller1Email = await SeedSellerWithStoreAsync("Store Alpha");
            var seller2Email = await SeedSellerWithStoreAsync("Store Beta");
            var product1Id = await CreateAndPublishProductAsync(seller1Email, "Product A", "Books", "15.99", "5");
            var product2Id = await CreateAndPublishProductAsync(seller2Email, "Product B", "Electronics", "49.99", "3");

            // Act: Login as buyer and add both products to cart
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(buyerEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/{product1Id}");
            await Page.GetByTestId("add-to-cart-btn").ClickAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/{product2Id}");
            await Page.GetByTestId("add-to-cart-btn").ClickAsync();

            // Assert: Cart shows both sellers
            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/cart", RegexOptions.IgnoreCase));
            var sellerGroups = Page.GetByTestId("seller-group");
            await Expect(sellerGroups).ToHaveCountAsync(2);
            await Expect(Page.GetByTestId("seller-name").First).ToContainTextAsync("Store Alpha");
            await Expect(Page.GetByTestId("seller-name").Last).ToContainTextAsync("Store Beta");
        }

        [Fact]
        public async Task CartPage_UpdateQuantity_UpdatesItemAndSubtotal()
        {
            // Arrange
            var buyerEmail = await SeedBuyerAsync();
            var sellerEmail = await SeedSellerAsync();
            var productId = await CreateAndPublishProductAsync(sellerEmail, "Test Product", "Books", "20.00", "10");

            // Act: Login and add product to cart
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(buyerEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/{productId}");
            await Page.GetByTestId("add-to-cart-btn").ClickAsync();

            // Update quantity to 3
            await Page.GetByTestId("cart-item-quantity").FillAsync("3");
            await Page.GetByTestId("cart-item-update").ClickAsync();

            // Assert: Quantity and subtotal updated
            await Expect(Page.GetByTestId("cart-item-quantity")).ToHaveValueAsync("3");
            await Expect(Page.GetByTestId("cart-item-subtotal")).ToContainTextAsync("60.00");
            await Expect(Page.GetByTestId("cart-total")).ToContainTextAsync("60.00");
        }

        [Fact]
        public async Task CartPage_RemoveItem_RemovesFromCart()
        {
            // Arrange
            var buyerEmail = await SeedBuyerAsync();
            var sellerEmail = await SeedSellerAsync();
            var productId = await CreateAndPublishProductAsync(sellerEmail, "Test Product", "Books", "20.00", "10");

            // Act: Login and add product to cart
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(buyerEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/{productId}");
            await Page.GetByTestId("add-to-cart-btn").ClickAsync();

            await Expect(Page.GetByTestId("cart-item")).ToBeVisibleAsync();

            // Remove item
            await Page.GetByTestId("cart-item-remove").ClickAsync();

            // Assert: Cart is empty
            await Expect(Page.GetByTestId("cart-empty")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("cart-item")).Not.ToBeVisibleAsync();
        }

        [Fact]
        public async Task AddToCart_IncreasesQuantity_WhenProductAlreadyInCart()
        {
            // Arrange
            var buyerEmail = await SeedBuyerAsync();
            var sellerEmail = await SeedSellerAsync();
            var productId = await CreateAndPublishProductAsync(sellerEmail, "Test Product", "Books", "25.00", "10");

            // Act: Login and add same product twice
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(buyerEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/{productId}");
            await Page.GetByTestId("add-to-cart-btn").ClickAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/products/{productId}");
            await Page.GetByTestId("add-to-cart-btn").ClickAsync();

            // Assert: Quantity is 2, not two separate items
            await Expect(Page.GetByTestId("cart-item")).ToHaveCountAsync(1);
            await Expect(Page.GetByTestId("cart-item-quantity")).ToHaveValueAsync("2");
            await Expect(Page.GetByTestId("cart-item-subtotal")).ToContainTextAsync("50.00");
        }

        private async Task<string> SeedBuyerAsync()
        {
            var email = $"buyer-{Guid.NewGuid():N}@example.com";
            var request = new
            {
                AccountType = AccountType.Buyer,
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

        private async Task<string> SeedSellerWithStoreAsync(string storeName)
        {
            var email = $"seller-{Guid.NewGuid():N}@example.com";
            var request = new
            {
                AccountType = AccountType.Seller,
                Email = email,
                Password = Password,
                EmailConfirmed = true,
                EnableTwoFactor = false,
                StoreName = storeName
            };

            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync($"{_fixture.BaseUrl}/_test/create-account", request);
            response.EnsureSuccessStatusCode();
            return email;
        }

        private async Task<int> CreateAndPublishProductAsync(string sellerEmail, string title, string category, string price, string stock)
        {
            // Login as seller
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(sellerEmail);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));

            // Create and publish product
            await Page.GotoAsync($"{_fixture.BaseUrl}/seller/products/create");
            await Page.GetByTestId("product-title").FillAsync(title);
            await Page.GetByTestId("product-category").FillAsync(category);
            await Page.GetByTestId("product-price").FillAsync(price);
            await Page.GetByTestId("product-stock").FillAsync(stock);
            await Page.GetByTestId("product-description").FillAsync($"Description for {title}");
            await Page.GetByTestId("product-publish").CheckAsync();
            await Page.GetByTestId("save-product").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/products", RegexOptions.IgnoreCase));

            // Get product ID from the list
            var productRow = Page.GetByTestId("seller-product-row").First;
            var editLink = productRow.GetByTestId("edit-product-link");
            var href = await editLink.GetAttributeAsync("href");
            var match = Regex.Match(href ?? "", @"/seller/products/(\d+)");
            var productId = int.Parse(match.Groups[1].Value);

            // Logout
            await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();

            return productId;
        }
    }
}
