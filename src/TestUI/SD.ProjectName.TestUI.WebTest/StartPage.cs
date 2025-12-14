using System;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class StartPage : PageTest, IClassFixture<WebAppFixture>
    {
        private readonly WebAppFixture _fixture;

        public StartPage(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task RegisterPage_AllowsSelectingAccountType()
        {
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Register");

            await Expect(Page.GetByTestId("account-type-buyer")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("account-type-seller")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task RegisterPage_ShowsValidationErrors_ForMissingSellerDetails()
        {
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Register");

            await Page.GetByTestId("account-type-seller").CheckAsync();
            await Page.GetByTestId("first-name").FillAsync("Sally");
            await Page.GetByTestId("last-name").FillAsync("Seller");
            await Page.GetByTestId("email").FillAsync($"seller-{Guid.NewGuid():N}@example.com");
            await Page.GetByTestId("password").FillAsync("S3cure!Passw0rd");
            await Page.GetByTestId("confirm-password").FillAsync("S3cure!Passw0rd");
            await Page.GetByTestId("accept-terms").CheckAsync();

            await Page.GetByTestId("submit-registration").ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/Register", RegexOptions.IgnoreCase), new() { Timeout = 15000 });
            await Expect(Page.Locator("body")).ToContainTextAsync("Company name is required for sellers.", new() { Timeout = 15000 });
            await Expect(Page.Locator("body")).ToContainTextAsync("Tax ID is required for sellers.", new() { Timeout = 15000 });
        }

        [Fact]
        public async Task RegisterPage_ShowsConfirmationAfterSuccessfulRegistration()
        {
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Register");

            var email = $"buyer-{Guid.NewGuid():N}@example.com";
            await Page.GetByTestId("account-type-buyer").CheckAsync();
            await Page.GetByTestId("first-name").FillAsync("Briana");
            await Page.GetByTestId("last-name").FillAsync("Buyer");
            await Page.GetByTestId("email").FillAsync(email);
            await Page.GetByTestId("password").FillAsync("S3cure!Passw0rd");
            await Page.GetByTestId("confirm-password").FillAsync("S3cure!Passw0rd");
            await Page.GetByTestId("accept-terms").CheckAsync();

            await Page.GetByTestId("submit-registration").ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(Page).ToHaveURLAsync(new Regex("RegisterConfirmation", RegexOptions.IgnoreCase), new() { Timeout = 15000 });
            await Expect(Page.GetByTestId("verification-message")).ToContainTextAsync("unverified");
            await Expect(Page.GetByTestId("verification-message")).ToContainTextAsync(email);
        }
    }
}
