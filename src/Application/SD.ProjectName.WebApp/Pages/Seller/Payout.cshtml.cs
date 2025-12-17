using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class PayoutModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PayoutModel> _logger;

        public PayoutModel(UserManager<ApplicationUser> userManager, ILogger<PayoutModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool RequiresKyc { get; private set; }

        public KycStatus KycStatus { get; private set; }

        public string? MaskedAccountNumber { get; private set; }

        public IEnumerable<SelectListItem> SupportedMethods => new[]
        {
            new SelectListItem("Bank transfer", PayoutMethod.BankTransfer.ToString(), Input.PreferredMethod == PayoutMethod.BankTransfer)
        };

        [TempData]
        public string? StatusMessage { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(200, MinimumLength = 3)]
            [Display(Name = "Payout beneficiary name")]
            public string PayoutBeneficiaryName { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 5)]
            [RegularExpression(@"^[A-Za-z0-9 \-]{5,}$", ErrorMessage = "Account number must contain at least 5 characters.")]
            [Display(Name = "Payout account number")]
            public string PayoutAccountNumber { get; set; } = string.Empty;

            [StringLength(120)]
            [Display(Name = "Bank name")]
            public string? PayoutBankName { get; set; }

            [Required]
            [Display(Name = "Default payout method")]
            public PayoutMethod PreferredMethod { get; set; } = PayoutMethod.BankTransfer;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (user.AccountType != AccountType.Seller)
            {
                return RedirectToPage("/Index");
            }

            if (!user.OnboardingCompleted)
            {
                return RedirectToPage("/Seller/Onboarding");
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;

            PopulateInput(user);
            MaskedAccountNumber = PayoutMasking.MaskAccountNumber(user.PayoutAccountNumber);

            StatusMessage ??= "Provide payout details in the required format. Only supported payout methods can be selected.";

            if (string.IsNullOrWhiteSpace(user.PayoutBeneficiaryName) || string.IsNullOrWhiteSpace(user.PayoutAccountNumber))
            {
                StatusMessage = "Add payout account details to receive transfers.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (user.AccountType != AccountType.Seller)
            {
                return RedirectToPage("/Index");
            }

            if (!user.OnboardingCompleted)
            {
                return RedirectToPage("/Seller/Onboarding");
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;

            if (!ModelState.IsValid)
            {
                MaskedAccountNumber = PayoutMasking.MaskAccountNumber(Input.PayoutAccountNumber);
                return Page();
            }

            var supportedValues = SupportedMethods
                .Select(m => m.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!supportedValues.Contains(Input.PreferredMethod.ToString()))
            {
                ModelState.AddModelError("Input.PreferredMethod", "Selected payout method is not supported in your region.");
                MaskedAccountNumber = PayoutMasking.MaskAccountNumber(Input.PayoutAccountNumber);
                return Page();
            }

            var requireReverification = user.KycStatus == KycStatus.Approved;

            user.PayoutBeneficiaryName = Input.PayoutBeneficiaryName.Trim();
            user.PayoutAccountNumber = Input.PayoutAccountNumber.Trim();
            user.PayoutBankName = string.IsNullOrWhiteSpace(Input.PayoutBankName) ? null : Input.PayoutBankName.Trim();
            user.PayoutDefaultMethod = Input.PreferredMethod;

            if (requireReverification)
            {
                user.KycStatus = KycStatus.Pending;
                user.KycApprovedAt = null;
                user.KycSubmittedAt = DateTimeOffset.UtcNow;
                user.RequiresKyc = true;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                MaskedAccountNumber = PayoutMasking.MaskAccountNumber(user.PayoutAccountNumber);
                return Page();
            }

            StatusMessage = requireReverification
                ? "Payout settings saved and pending verification."
                : "Payout settings updated.";

            return RedirectToPage();
        }

        private void PopulateInput(ApplicationUser user)
        {
            Input.PayoutBeneficiaryName = user.PayoutBeneficiaryName ?? $"{user.FirstName} {user.LastName}".Trim();
            Input.PayoutAccountNumber = user.PayoutAccountNumber ?? string.Empty;
            Input.PayoutBankName = user.PayoutBankName ?? string.Empty;
            Input.PreferredMethod = user.PayoutDefaultMethod;
        }
    }
}
