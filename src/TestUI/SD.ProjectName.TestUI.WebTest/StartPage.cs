using Microsoft.Playwright.Xunit;
using System.Text.RegularExpressions;

namespace SD.ProjectName.TestUI.WebTest
{
    public class StartPage : PageTest
    {
        [Fact]
        public async Task HasTitle()
        {
            await Page.GotoAsync("https://localhost:7045/");

            // Expect a title "to contain" a substring.
            await Expect(Page).ToHaveURLAsync(new Regex("Product list"));
        }

    }
}
