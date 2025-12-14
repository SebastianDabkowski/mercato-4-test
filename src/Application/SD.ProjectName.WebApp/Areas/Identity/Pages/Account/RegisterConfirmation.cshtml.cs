using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.WebApp.Areas.Identity.Pages.Account
{
    public class RegisterConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public RegisterConfirmationModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string Email { get; private set; } = string.Empty;

        public AccountType? AccountType { get; private set; }

        public async Task<IActionResult> OnGetAsync(string? email = null, AccountType? accountType = null)
        {
            if (email is null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user is null)
            {
                return NotFound($"Unable to load user with email '{email}'.");
            }

            Email = email;
            AccountType = accountType ?? user.AccountType;

            return Page();
        }
    }
}
