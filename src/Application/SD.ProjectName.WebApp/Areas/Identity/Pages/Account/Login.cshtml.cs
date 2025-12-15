using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public class LoginModel : PageModel
    {
        private const string InvalidCredentialsMessage = "Invalid email or password.";
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public bool ShowEmailVerificationRequired { get; private set; }

        public bool VerificationEmailResent { get; private set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Keep me signed in on this device")]
            public bool RememberMe { get; set; } = true;
        }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
            {
                await _signInManager.SignOutAsync();
            }
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ReturnUrl = returnUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var email = Input.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            if (user is null)
            {
                ModelState.AddModelError(string.Empty, InvalidCredentialsMessage);
                return Page();
            }

            if (user.AccountType == AccountType.Seller && !user.EmailConfirmed)
            {
                await SendVerificationEmailAsync(user);
                ShowEmailVerificationRequired = true;
                ModelState.AddModelError(string.Empty, "Verify your seller email to continue.");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, Input.Password, Input.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");
                var redirect = await ResolveRedirectAsync(user, NormalizeReturnUrl(returnUrl));
                return LocalRedirect(redirect);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = ReturnUrl, Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                ModelState.AddModelError(string.Empty, "This account is locked because of too many failed attempts. Try again later.");
                return Page();
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "You must confirm your email before signing in.");
                return Page();
            }

            ModelState.AddModelError(string.Empty, InvalidCredentialsMessage);
            return Page();
        }

        public async Task<IActionResult> OnPostResendVerificationAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            if (string.IsNullOrWhiteSpace(Input.Email))
            {
                ModelState.AddModelError(string.Empty, "Enter your email to resend verification.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (user is null)
            {
                ModelState.AddModelError(string.Empty, InvalidCredentialsMessage);
                return Page();
            }

            await SendVerificationEmailAsync(user);
            ShowEmailVerificationRequired = true;
            ModelState.AddModelError(string.Empty, "We sent a new verification email to you.");
            return Page();
        }

        private async Task SendVerificationEmailAsync(ApplicationUser user)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code, returnUrl = ReturnUrl },
                protocol: Request.Scheme);

            user.EmailVerificationSentAt = DateTimeOffset.UtcNow;
            await _userManager.UpdateAsync(user);

            await _emailSender.SendEmailAsync(user.Email!, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>clicking here</a>.");

            VerificationEmailResent = true;
        }

        private async Task<string> ResolveRedirectAsync(ApplicationUser user, string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            if (user.AccountType == AccountType.Seller)
            {
                if (user.RequiresKyc && user.KycStatus != KycStatus.Approved)
                {
                    return Url.Content("~/seller/kyc")!;
                }

                return Url.Content("~/seller/dashboard")!;
            }

            if (user.AccountType == AccountType.Buyer)
            {
                return Url.Content("~/buyer/dashboard")!;
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(IdentityRoles.Seller))
            {
                if (user.RequiresKyc && user.KycStatus != KycStatus.Approved)
                {
                    return Url.Content("~/seller/kyc")!;
                }

                return Url.Content("~/seller/dashboard")!;
            }

            if (roles.Contains(IdentityRoles.Buyer))
            {
                return Url.Content("~/buyer/dashboard")!;
            }

            return Url.Content("~/")!;
        }

        private string? NormalizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return null;
            }

            var rootUrl = Url.Content("~/");
            return string.Equals(returnUrl, rootUrl, StringComparison.OrdinalIgnoreCase) ? null : returnUrl;
        }
    }
}
