using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;
using SD.ProjectName.WebApp.Stores;

namespace SD.ProjectName.WebApp.Pages.Seller
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FeatureFlags _featureFlags;
        private readonly PayoutScheduleService _payoutScheduleService;

        public DashboardModel(
            UserManager<ApplicationUser> userManager,
            IOptions<FeatureFlags> featureOptions,
            PayoutScheduleService payoutScheduleService)
        {
            _userManager = userManager;
            _featureFlags = featureOptions.Value;
            _payoutScheduleService = payoutScheduleService;
        }

        public bool RequiresKyc { get; private set; }

        public KycStatus KycStatus { get; private set; }

        public string? StoreName { get; private set; }

        public string? PublicStoreUrl { get; private set; }

        public bool HasPayoutDetails { get; private set; }

        public string? MaskedPayoutAccount { get; private set; }

        public PayoutMethod PayoutDefaultMethod { get; private set; } = PayoutMethod.BankTransfer;

        public PayoutScheduleSummary? LatestPayout { get; private set; }

        public bool SellerUserManagementEnabled { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (!user.OnboardingCompleted)
            {
                return RedirectToPage("/Seller/Onboarding");
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;

            if (user.RequiresKyc && user.KycStatus != KycStatus.Approved)
            {
                if (user.KycStatus == KycStatus.Pending)
                {
                    StatusMessage ??= "Your onboarding is pending verification.";
                }
                else
                {
                    StatusMessage ??= "Complete KYC to access seller tools.";
                    return RedirectToPage("/Seller/Kyc");
                }
            }

            StoreName = user.StoreName;
            if (!string.IsNullOrWhiteSpace(user.StoreName))
            {
                var slug = StoreUrlHelper.ToSlug(user.StoreName);
                if (!string.IsNullOrWhiteSpace(slug))
                {
                    PublicStoreUrl = Url.Page("/Stores/Profile", pageHandler: null, values: new { storeSlug = slug }, protocol: Request.Scheme);
                }
            }

            HasPayoutDetails = !string.IsNullOrWhiteSpace(user.PayoutBeneficiaryName) && !string.IsNullOrWhiteSpace(user.PayoutAccountNumber);
            MaskedPayoutAccount = PayoutMasking.MaskAccountNumber(user.PayoutAccountNumber);
            PayoutDefaultMethod = user.PayoutDefaultMethod;

            if (!HasPayoutDetails)
            {
                StatusMessage ??= "Add payout details to receive transfers.";
            }
            else
            {
                var schedules = await _payoutScheduleService.GetRecentSchedulesForSellerAsync(user.Id, 1);
                var latest = schedules.FirstOrDefault();
                if (latest is not null)
                {
                    LatestPayout = new PayoutScheduleSummary
                    {
                        Status = latest.Status,
                        Amount = latest.TotalAmount,
                        ScheduledFor = latest.ScheduledFor,
                        PaidAt = latest.PaidAt,
                        ErrorReference = latest.ErrorReference,
                        ProcessingStartedAt = latest.ProcessingStartedAt
                    };
                }
            }

            SellerUserManagementEnabled = _featureFlags.EnableSellerInternalUsers;

            return Page();
        }

        public class PayoutScheduleSummary
        {
            public string Status { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public DateTimeOffset ScheduledFor { get; set; }
            public DateTimeOffset? PaidAt { get; set; }
            public string? ErrorReference { get; set; }
            public DateTimeOffset? ProcessingStartedAt { get; set; }

            public string StatusLabel => Status switch
            {
                PayoutStatus.Paid => "Paid",
                PayoutStatus.Processing => "Processing",
                PayoutStatus.Failed => "Failed",
                _ => "Scheduled"
            };
        }
    }
}
