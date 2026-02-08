using System.ComponentModel.DataAnnotations;
using System.Linq;
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
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;
using SD.ProjectName.WebApp.Services;

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
        private readonly ILoginEventLogger _loginEventLogger;
        private readonly CartMergeService _cartMergeService;
        private readonly ICartIdentityService _cartIdentityService;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<LoginModel> logger,
            ILoginEventLogger loginEventLogger,
            CartMergeService cartMergeService,
            ICartIdentityService cartIdentityService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
            _loginEventLogger = loginEventLogger;
            _cartMergeService = cartMergeService;
            _cartIdentityService = cartIdentityService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; private set; } = new List<AuthenticationScheme>();

        public bool ShowEmailVerificationRequired { get; private set; }

        public bool VerificationEmailResent { get; private set; }

        [TempData]
        public string? ErrorMessage { get; set; }

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
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

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

            var ipAddress = RequestMetadataHelper.GetClientIp(HttpContext);
            var userAgent = RequestMetadataHelper.GetUserAgent(HttpContext);
            var guestBuyerId = _cartIdentityService.GetGuestBuyerId();

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
                await _loginEventLogger.LogAsync(user, LoginEventType.PasswordSignInSuccess, true, ipAddress, userAgent, cancellationToken: HttpContext.RequestAborted);
                if (!string.IsNullOrWhiteSpace(guestBuyerId))
                {
                    await _cartMergeService.MergeAsync(guestBuyerId, user.Id);
                    _cartIdentityService.ClearGuestBuyerId();
                }
                var redirect = await ResolveRedirectAsync(user, NormalizeReturnUrl(returnUrl));
                return LocalRedirect(redirect);
            }

            if (result.RequiresTwoFactor)
            {
                await _loginEventLogger.LogAsync(user, LoginEventType.TwoFactorChallenge, true, ipAddress, userAgent, cancellationToken: HttpContext.RequestAborted);
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = ReturnUrl, Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                await _loginEventLogger.LogAsync(user, LoginEventType.AccountLockedOut, false, ipAddress, userAgent, "Account locked after failed attempts.", HttpContext.RequestAborted);
                ModelState.AddModelError(string.Empty, "This account is locked because of too many failed attempts. Try again later.");
                return Page();
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "You must confirm your email before signing in.");
                await _loginEventLogger.LogAsync(user, LoginEventType.PasswordSignInFailure, false, ipAddress, userAgent, "Email not confirmed.", HttpContext.RequestAborted);
                return Page();
            }

            ModelState.AddModelError(string.Empty, InvalidCredentialsMessage);
            await _loginEventLogger.LogAsync(user, LoginEventType.PasswordSignInFailure, false, ipAddress, userAgent, "Invalid credentials.", HttpContext.RequestAborted);
            return Page();
        }

        public async Task<IActionResult> OnPostResendVerificationAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
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

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains(IdentityRoles.Admin))
            {
                return Url.Content("~/admin/dashboard")!;
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
