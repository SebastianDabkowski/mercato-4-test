using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.WebApp.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ConfirmEmailModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [TempData]
        public string StatusMessage { get; set; } = string.Empty;

        public string? ReturnUrl { get; private set; }

        public string? UserEmail { get; private set; }

        public string? UserId { get; private set; }

        public bool CanResendVerification { get; private set; }

        public async Task<IActionResult> OnGetAsync(string? userId, string? code, string? returnUrl = null)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            ReturnUrl = returnUrl;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            UserEmail = user.Email;
            UserId = user.Id;

            if (user.EmailConfirmed)
            {
                StatusMessage = "Your email is already confirmed.";
                return RedirectAfterSuccess(returnUrl);
            }

            string decodedCode;
            try
            {
                decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                StatusMessage = "This verification link is invalid or has expired. You can request a new verification email below.";
                CanResendVerification = true;
                return Page();
            }

            var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
            if (result.Succeeded)
            {
                if (user.AccountStatus == AccountStatus.Unverified)
                {
                    user.AccountStatus = AccountStatus.Verified;
                }

                user.EmailVerifiedAt ??= DateTimeOffset.UtcNow;
                await _userManager.UpdateAsync(user);
            }

            StatusMessage = result.Succeeded
                ? "Thank you for confirming your email. Your account is now verified."
                : "This verification link is invalid or has expired. You can request a new verification email below.";

            CanResendVerification = !result.Succeeded;

            if (!string.IsNullOrEmpty(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostResendAsync(string? userId, string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            if (user.EmailConfirmed)
            {
                StatusMessage = "Your email is already confirmed.";
                return RedirectAfterSuccess(returnUrl);
            }

            user.EmailVerificationSentAt = DateTimeOffset.UtcNow;
            await _userManager.UpdateAsync(user);

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code, returnUrl },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(user.Email!, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>clicking here</a>.");

            StatusMessage = "We sent you a fresh verification email. Please check your inbox.";
            UserEmail = user.Email;
            UserId = user.Id;
            CanResendVerification = false;

            return Page();
        }

        private IActionResult RedirectAfterSuccess(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Page();
        }
    }
}
