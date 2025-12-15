using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public bool RequiresKyc { get; private set; }

        public KycStatus KycStatus { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;

            if (user.RequiresKyc && user.KycStatus != KycStatus.Approved)
            {
                StatusMessage ??= "Complete KYC to access seller tools.";
                return RedirectToPage("/Seller/Kyc");
            }

            return Page();
        }
    }
}
