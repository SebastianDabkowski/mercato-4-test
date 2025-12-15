using System;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.TestUI.WebTest
{
    [Collection("playwright-webapp")]
    public class PasswordResetPage : PageTest, IClassFixture<WebAppFixture>
    {
        private const string Password = "S3cure!Passw0rd";
        private readonly WebAppFixture _fixture;

        public PasswordResetPage(WebAppFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ForgotPassword_ShowsGenericConfirmation()
        {
            var email = $"unknown-{Guid.NewGuid():N}@example.com";

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/ForgotPassword");
            await Expect(Page.GetByTestId("forgot-email")).ToBeVisibleAsync(new() { Timeout = 15000 });

            await Page.GetByTestId("forgot-email").FillAsync(email);
            await Page.GetByTestId("forgot-submit").ClickAsync();

            await Expect(Page.GetByTestId("reset-link-message")).ToContainTextAsync("If an account exists", new() { Timeout = 15000 });
        }

        [Fact]
        public async Task ResetPassword_InvalidLink_ShowsErrorAndResendOption()
        {
            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/ResetPassword?code=invalid&userId=missing");

            await Expect(Page.GetByTestId("invalid-reset-link")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Expect(Page.GetByTestId("request-new-link")).ToBeVisibleAsync();
        }

        [Fact]
        public async Task ChangePassword_UpdatesCredentials()
        {
            var email = await SeedUserAsync(AccountType.Buyer, emailConfirmed: true);
            var newPassword = "N3w!Passw0rd123";

            ConfigureTimeouts();
            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));

            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Manage/ChangePassword");
            await Expect(Page.GetByTestId("current-password")).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Page.GetByTestId("current-password").FillAsync(Password);
            await Page.GetByTestId("new-password").FillAsync(newPassword);
            await Page.GetByTestId("confirm-new-password").FillAsync(newPassword);
            await Page.GetByTestId("change-password-submit").ClickAsync();

            await Expect(Page.GetByTestId("change-password-success")).ToBeVisibleAsync(new() { Timeout = 15000 });

            await Page.GetByText("Logout").ClickAsync();

            await Page.GotoAsync($"{_fixture.BaseUrl}/Identity/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Page.GetByTestId("login-email").FillAsync(email);
            await Page.GetByTestId("login-password").FillAsync(Password);
            await Page.GetByTestId("login-submit").ClickAsync();
            await Expect(Page.Locator("body")).ToContainTextAsync("Invalid email or password.");

            await Page.GetByTestId("login-password").FillAsync(newPassword);
            await Page.GetByTestId("login-submit").ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/buyer/dashboard", RegexOptions.IgnoreCase));
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
