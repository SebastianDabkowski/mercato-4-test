using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class LoginPage : PageTest, IClassFixture<WebAppFixture>
    {
        private const string Password = "S3cure!Passw0rd";
        private readonly WebAppFixture _fixture;

        public LoginPage(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Buyer_IsRedirectedToDashboard_OnSuccessfulLogin()
        {
            var email = await SeedUserAsync(AccountType.Buyer, emailConfirmed: true);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/Login", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));
            await Expect(Page.GetByText($"Hello {email}!")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task SocialLogin_CreatesBuyerAccount_WhenEmailIsNew()
        {
            var email = $"social-buyer-{Guid.NewGuid():N}@example.com";
            await SetFakeExternalEmailAsync("google", email);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByTestId("google-login")).ToBeVisibleAsync(new() { Timeout = 15000 });

            await Page.GetByTestId("google-login").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));
            await Expect(Page.GetByText($"Hello {email}!")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task SocialLogin_UsesExistingBuyerAccount()
        {
            var email = await SeedUserAsync(AccountType.Buyer, emailConfirmed: false);
            await SetFakeExternalEmailAsync("facebook", email);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByTestId("facebook-login")).ToBeVisibleAsync(new() { Timeout = 15000 });

            await Page.GetByTestId("facebook-login").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));
            await Expect(Page.GetByText($"Hello {email}!")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task SocialLogin_ShowsError_ForSellerAccount()
        {
            var email = await SeedUserAsync(AccountType.Seller, emailConfirmed: true);
            await SetFakeExternalEmailAsync("google", email);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByTestId("google-login")).ToBeVisibleAsync(new() { Timeout = 15000 });

            await Page.GetByTestId("google-login").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/Login", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("external-error")).ToContainTextAsync("buyers", new() { Timeout = 15000 });
        }

        [Fact]
        public async Task SocialLogin_ShowsMessage_WhenProviderFails()
        {
            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/ExternalLogin?handler=Callback&remoteError=access_denied&returnUrl=%2Fbuyer%2Fdashboard");

            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/Login", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("external-error")).ToContainTextAsync("failed", new() { Timeout = 15000 });
        }

        [Fact]
        public async Task ShowsGenericError_ForInvalidCredentials()
        {
            var email = await SeedUserAsync(AccountType.Buyer, emailConfirmed: true);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/Login", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync("WrongP@ssword123");
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/Login", RegexOptions.IgnoreCase));
            await Expect(Page.Locator("body")).ToContainTextAsync("Invalid email or password.");
        }

        [Fact]
        public async Task SellerSeesVerificationPrompt_WhenEmailUnverified()
        {
            var email = await SeedUserAsync(AccountType.Seller, emailConfirmed: false);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/Login", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page.GetByTestId("seller-verification-alert")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("resend-verification-link")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task SessionPersistsAcrossNewContextWithinLifetime()
        {
            var email = await SeedUserAsync(AccountType.Buyer, emailConfirmed: true);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/Login", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));

            var state = await Context.StorageStateAsync();

            await using var newContext = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                StorageState = state
            });
            var newPage = await newContext.NewPageAsync();
            await newPage.GotoAsync($"{_fixture.BaseUrl}/buyer/dashboard");

            await Expect(newPage.GetByText($"Hello {email}!")).ToBeVisibleAsync();
            await newContext.CloseAsync();
        }

        [Fact]
        public async Task SellerMustCompleteOnboardingBeforeDashboard()
        {
            var email = await SeedUserAsync(AccountType.Seller, emailConfirmed: true);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

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
            await Page.GetByTestId("onboarding-account-number").FillAsync("PL00123456789");
            await Page.GetByTestId("onboarding-bank-name").FillAsync("Test Bank");
            await Page.GetByTestId("onboarding-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("seller-status")).ToContainTextAsync("pending", new() { Timeout = 15000 });
        }

        [Fact]
        public async Task SellerCompanyCompletesOnboardingAndSeesPendingStatus()
        {
            var email = await SeedUserAsync(AccountType.Seller, emailConfirmed: true);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/onboarding", RegexOptions.IgnoreCase));
            await Page.GetByTestId("onboarding-store-name").FillAsync($"Store-{Guid.NewGuid():N}");
            await Page.GetByTestId("onboarding-contact-email").FillAsync("contact@example.com");
            await Page.GetByTestId("onboarding-description").FillAsync("Company store");
            await Page.GetByTestId("onboarding-continue").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/onboarding/2", RegexOptions.IgnoreCase));
            await Page.GetByTestId("onboarding-seller-type-company").CheckAsync();
            await Expect(Page.GetByTestId("onboarding-company-name")).ToBeVisibleAsync();
            await Page.GetByTestId("onboarding-company-name").FillAsync("Acme Sp. z o.o.");
            await Page.GetByTestId("onboarding-registration-number").FillAsync("REG-445566");
            await Page.GetByTestId("onboarding-tax-id").FillAsync("TAX-123-456");
            await Page.GetByTestId("onboarding-contact-person").FillAsync("Alex Manager");
            await Page.GetByTestId("onboarding-registered-address").FillAsync("Industrial Park 1, Warsaw");
            await Page.GetByTestId("onboarding-verification-phone").FillAsync("+48111222333");
            await Page.GetByTestId("onboarding-continue").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/onboarding/3", RegexOptions.IgnoreCase));
            await Page.GetByTestId("onboarding-beneficiary").FillAsync("Acme Sp. z o.o.");
            await Page.GetByTestId("onboarding-account-number").FillAsync("PL00999888777");
            await Page.GetByTestId("onboarding-bank-name").FillAsync("Corporate Bank");
            await Page.GetByTestId("onboarding-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/seller/dashboard", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("seller-status")).ToContainTextAsync("pending", new() { Timeout = 15000 });
        }

        private async Task<string> SeedUserAsync(AccountType accountType, bool emailConfirmed)
        {
            var email = $"{accountType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
            var request = new
            {
                AccountType = accountType,
                Email = email,
                Password = Password,
                EmailConfirmed = emailConfirmed
            };

            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync($"{_fixture.BaseUrl}/_test/create-account", request);
            response.EnsureSuccessStatusCode();

            return email;
        }

        private Task SetFakeExternalEmailAsync(string provider, string email)
        {
            return Page.Context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = $"fake-{provider}-email",
                    Value = email,
                    Url = _fixture.BaseUrl
                }
            });
        }

        private void ConfigureTimeouts()
        {
            Page.SetDefaultTimeout(15000);
            Page.SetDefaultNavigationTimeout(15000);
        }
    }
}
