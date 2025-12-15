using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class KycModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public KycModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public KycStatus KycStatus { get; private set; }

        public bool RequiresKyc { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(200)]
            [Display(Name = "Legal name on document")]
            public string LegalName { get; set; } = string.Empty;

            [Required]
            [StringLength(100)]
            [Display(Name = "Government ID number")]
            public string DocumentNumber { get; set; } = string.Empty;

            [Required]
            [StringLength(100)]
            [Display(Name = "Country of issue")]
            public string Country { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;

            if (!user.RequiresKyc || user.KycStatus == KycStatus.Approved)
            {
                return RedirectToPage("/Seller/Dashboard");
            }

            Input.LegalName = $"{user.FirstName} {user.LastName}".Trim();
            StatusMessage ??= "Complete KYC to unlock seller tools.";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;

            if (!user.RequiresKyc || user.KycStatus == KycStatus.Approved)
            {
                return RedirectToPage("/Seller/Dashboard");
            }

            if (!ModelState.IsValid)
            {
                StatusMessage = "Provide all required details to continue with KYC.";
                return Page();
            }

            user.KycStatus = KycStatus.Approved;
            user.KycSubmittedAt ??= DateTimeOffset.UtcNow;
            user.KycApprovedAt = DateTimeOffset.UtcNow;

            await _userManager.UpdateAsync(user);

            TempData["StatusMessage"] = "Your KYC is approved. You can now access seller tools.";

            return RedirectToPage("/Seller/Dashboard");
        }
    }
}
