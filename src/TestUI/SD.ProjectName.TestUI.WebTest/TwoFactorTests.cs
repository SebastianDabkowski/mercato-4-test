using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class TwoFactorTests : PageTest, IClassFixture<WebAppFixture>
    {
        private const string Password = "S3cure!Passw0rd";
        private readonly WebAppFixture _fixture;

        public TwoFactorTests(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task BuyerCompletesEmailTwoFactorFlow()
        {
            var email = await SeedUserAsync(AccountType.Buyer, emailConfirmed: true, enableTwoFactor: true);

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByTestId("login-email")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/Identity/Account/LoginWith2fa", RegexOptions.IgnoreCase));
            await Expect(Page.GetByTestId("twofactor-title")).ToBeVisibleAsync(new() { Timeout = 15000 });

            var code = await FetchTwoFactorCodeAsync(email);
            await Page.GetByTestId("twofactor-code").FillAsync(code);
            await Page.GetByTestId("twofactor-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));
            await Expect(Page.GetByText($"Hello {email}!")).ToBeVisibleAsync();
        }

        private async Task<string> FetchTwoFactorCodeAsync(string email)
        {
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync($"{_fixture.BaseUrl}/_test/generate-2fa-code", new { Email = email });
            response.EnsureSuccessStatusCode();
            var code = await response.Content.ReadAsStringAsync();
            return code.Trim('"');
        }

        private async Task<string> SeedUserAsync(AccountType accountType, bool emailConfirmed, bool enableTwoFactor)
        {
            var email = $"{accountType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
            var request = new
            {
                AccountType = accountType,
                Email = email,
                Password = Password,
                EmailConfirmed = emailConfirmed,
                EnableTwoFactor = enableTwoFactor
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
