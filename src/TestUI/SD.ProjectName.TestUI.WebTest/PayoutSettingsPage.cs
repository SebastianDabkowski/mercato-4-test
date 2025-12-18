using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class PayoutSettingsPage : PageTest, IClassFixture<WebAppFixture>
    {
        private const string Password = "S3cure!Passw0rd";
        private readonly WebAppFixture _fixture;

        public PayoutSettingsPage(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SellerCanUpdatePayoutSettings()
        {
            Page.SetDefaultTimeout(15000);
            var email = await SeedSellerAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await CompleteOnboardingAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));
            await Page.GetByTestId("payout-settings-link").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/payout", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("payout-method")).ToHaveValueAsync(PayoutMethod.BankTransfer.ToString());
            await Expect(Page.GetByTestId("payout-account-number")).ToHaveValueAsync("PL00999888777");

            await Page.GetByTestId("payout-beneficiary").FillAsync("Updated Beneficiary");
            await Page.GetByTestId("payout-account-number").FillAsync("PL445566001122");
            await Page.GetByTestId("payout-bank-name").FillAsync("Security Bank");
            await Page.GetByTestId("payout-save").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/payout", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("payout-status")).ToContainTextAsync("saved", new() { Timeout = 15000 });
            await Expect(Page.GetByTestId("payout-account-number")).ToHaveValueAsync("PL445566001122");
        }

        private async Task CompleteOnboardingAsync()
        {
            await Expect(Page).ToHaveURLAsync(new Regex("/seller/onboarding", RegexOptions.IgnoreCase));
            await Page.GetByTestId("onboarding-store-name").FillAsync($"Store-{Guid.NewGuid():N}");
            await Page.GetByTestId("onboarding-contact-email").FillAsync("contact@example.com");
            await Page.GetByTestId("onboarding-description").FillAsync("Test store");
            await Page.GetByTestId("onboarding-continue").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/onboarding/2", RegexOptions.IgnoreCase));
            await Page.GetByTestId("onboarding-seller-type-individual").CheckAsync();
            await Page.GetByTestId("onboarding-full-name").FillAsync("Seller Example");
            await Page.GetByTestId("onboarding-personal-id").FillAsync("ID12345");
            await Page.GetByTestId("onboarding-registered-address").FillAsync("Main Street 12, Cityville");
            await Page.GetByTestId("onboarding-verification-phone").FillAsync("+48123123123");
            await Page.GetByTestId("onboarding-continue").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/onboarding/3", RegexOptions.IgnoreCase));
            await Page.GetByTestId("onboarding-beneficiary").FillAsync("Seller Example");
            await Page.GetByTestId("onboarding-account-number").FillAsync("PL00999888777");
            await Page.GetByTestId("onboarding-bank-name").FillAsync("Corporate Bank");
            await Page.GetByTestId("onboarding-submit").ClickAsync();
        }

        private async Task<string> SeedSellerAsync()
        {
            var email = $"payout-{Guid.NewGuid():N}@example.com";
            var request = new
            {
                AccountType = AccountType.Seller,
                Email = email,
                Password = Password,
                EmailConfirmed = true
            };

            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync($"{_fixture.BaseUrl}/_test/create-account", request);
            response.EnsureSuccessStatusCode();

            return email;
        }
    }
}
