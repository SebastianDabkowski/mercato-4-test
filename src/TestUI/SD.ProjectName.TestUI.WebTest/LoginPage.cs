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

        private void ConfigureTimeouts()
        {
            Page.SetDefaultTimeout(15000);
            Page.SetDefaultNavigationTimeout(15000);
        }
    }
}
