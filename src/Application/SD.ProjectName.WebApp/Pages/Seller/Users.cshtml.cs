using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SD.ProjectName.WebApp;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class UsersModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly FeatureFlags _featureFlags;

        public UsersModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IOptions<FeatureFlags> featureOptions)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _featureFlags = featureOptions.Value;
        }

        [BindProperty]
        public InviteInputModel InviteInput { get; set; } = new();

        public IList<InternalUserViewModel> InternalUsers { get; private set; } = new List<InternalUserViewModel>();

        public bool FeatureEnabled => _featureFlags.EnableSellerInternalUsers;

        [TempData]
        public string? StatusMessage { get; set; }

        public class InviteInputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email address")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Role")]
            public SellerTeamRole Role { get; set; } = SellerTeamRole.CatalogManager;
        }

        public class InternalUserViewModel
        {
            public string Id { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;

            public SellerTeamRole Role { get; set; }

            public string Status { get; set; } = string.Empty;

            public bool IsOwner { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var owner = await _userManager.GetUserAsync(User);
            if (owner is null)
            {
                return Challenge();
            }

            await LoadInternalUsers(owner);

            return Page();
        }

        public async Task<IActionResult> OnPostInviteAsync()
        {
            var owner = await _userManager.GetUserAsync(User);
            if (owner is null)
            {
                return Challenge();
            }

            if (!FeatureEnabled)
            {
                StatusMessage = "Internal user management is not enabled.";
                await LoadInternalUsers(owner);
                return Page();
            }

            if (owner.SellerRole != SellerTeamRole.StoreOwner)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                await LoadInternalUsers(owner);
                return Page();
            }

            var trimmedEmail = InviteInput.Email.Trim();
            var existing = await _userManager.FindByEmailAsync(trimmedEmail);
            if (existing is not null)
            {
                if (existing.Id != owner.Id && existing.StoreOwnerId != owner.Id)
                {
                    ModelState.AddModelError(string.Empty, "This email is already in use.");
                    await LoadInternalUsers(owner);
                    return Page();
                }

                if (existing.Id == owner.Id)
                {
                    StatusMessage = "The store owner already has full access.";
                    await LoadInternalUsers(owner);
                    return Page();
                }

                existing.SellerRole = InviteInput.Role;
                existing.StoreOwnerId ??= owner.Id;
                existing.AccountStatus = existing.AccountStatus == AccountStatus.Suspended ? AccountStatus.Unverified : existing.AccountStatus;

                var updateExisting = await _userManager.UpdateAsync(existing);
                if (!updateExisting.Succeeded)
                {
                    AddErrors(updateExisting);
                    await LoadInternalUsers(owner);
                    return Page();
                }

                StatusMessage = "Updated existing user's role.";
                await LoadInternalUsers(owner);
                return Page();
            }

            var invitedUser = new ApplicationUser
            {
                Email = trimmedEmail,
                UserName = trimmedEmail,
                FirstName = "Pending",
                LastName = "User",
                AccountType = AccountType.Seller,
                AccountStatus = AccountStatus.Unverified,
                TermsAcceptedAt = DateTimeOffset.UtcNow,
                RequiresKyc = false,
                KycStatus = KycStatus.Approved,
                OnboardingStep = OnboardingStep.Completed,
                OnboardingCompleted = true,
                SellerRole = InviteInput.Role,
                StoreOwnerId = owner.Id,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(invitedUser);
            if (!createResult.Succeeded)
            {
                AddErrors(createResult);
                await LoadInternalUsers(owner);
                return Page();
            }

            await _userManager.AddToRoleAsync(invitedUser, IdentityRoles.Seller);

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(invitedUser);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encodedToken, email = invitedUser.Email },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                invitedUser.Email!,
                "You have been invited to the seller panel",
                $"You have been invited to manage {(owner.StoreName ?? "this store")}. Set your password to accept: <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>accept invitation</a>.");

            invitedUser.EmailVerificationSentAt = DateTimeOffset.UtcNow;
            await _userManager.UpdateAsync(invitedUser);

            StatusMessage = "Invitation sent.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangeRoleAsync(string userId, SellerTeamRole role)
        {
            var owner = await _userManager.GetUserAsync(User);
            if (owner is null)
            {
                return Challenge();
            }

            if (!FeatureEnabled)
            {
                return NotFound();
            }

            if (owner.SellerRole != SellerTeamRole.StoreOwner)
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || (user.Id != owner.Id && user.StoreOwnerId != owner.Id))
            {
                return NotFound();
            }

            if (user.Id == owner.Id)
            {
                StatusMessage = "You cannot change the store owner's role.";
                await LoadInternalUsers(owner);
                return Page();
            }

            user.SellerRole = role;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                AddErrors(result);
                await LoadInternalUsers(owner);
                return Page();
            }

            StatusMessage = "Role updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeactivateAsync(string userId)
        {
            var owner = await _userManager.GetUserAsync(User);
            if (owner is null)
            {
                return Challenge();
            }

            if (!FeatureEnabled)
            {
                return NotFound();
            }

            if (owner.SellerRole != SellerTeamRole.StoreOwner)
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || (user.Id != owner.Id && user.StoreOwnerId != owner.Id))
            {
                return NotFound();
            }

            if (user.Id == owner.Id)
            {
                StatusMessage = "Store owner cannot be deactivated.";
                await LoadInternalUsers(owner);
                return Page();
            }

            user.AccountStatus = AccountStatus.Suspended;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                AddErrors(result);
                await LoadInternalUsers(owner);
                return Page();
            }

            await _userManager.UpdateSecurityStampAsync(user);

            StatusMessage = "User deactivated and sessions revoked.";
            return RedirectToPage();
        }

        private async Task LoadInternalUsers(ApplicationUser owner)
        {
            InternalUsers = await _userManager.Users
                .Where(u => u.Id == owner.Id || u.StoreOwnerId == owner.Id)
                .OrderBy(u => u.Id == owner.Id ? 0 : 1)
                .ThenBy(u => u.Email)
                .Select(u => new InternalUserViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    Role = u.SellerRole,
                    Status = ResolveStatus(u),
                    IsOwner = u.Id == owner.Id
                })
                .ToListAsync();
        }

        private static string ResolveStatus(ApplicationUser user)
        {
            if (user.AccountStatus == AccountStatus.Suspended || (user.LockoutEnd.HasValue && user.LockoutEnd.Value.UtcDateTime > DateTime.UtcNow))
            {
                return "Deactivated";
            }

            if (!user.EmailConfirmed || user.AccountStatus == AccountStatus.Unverified)
            {
                return "Pending";
            }

            return "Active";
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
