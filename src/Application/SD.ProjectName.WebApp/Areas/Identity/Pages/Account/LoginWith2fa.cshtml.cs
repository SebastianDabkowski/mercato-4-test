using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private const string DevelopmentBypassCode = "000000";
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<LoginWith2faModel> _logger;
        private readonly ILoginEventLogger _loginEventLogger;
        private readonly TimeProvider _timeProvider;
        private readonly IHostEnvironment _environment;

        public LoginWith2faModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<LoginWith2faModel> logger,
            ILoginEventLogger loginEventLogger,
            TimeProvider timeProvider,
            IHostEnvironment environment)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
            _loginEventLogger = loginEventLogger;
            _timeProvider = timeProvider;
            _environment = environment;
        }

        public string? ReturnUrl { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool RememberMe { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Authentication code")]
            public string TwoFactorCode { get; set; } = string.Empty;

            [Display(Name = "Remember this device")]
            public bool RememberMachine { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            RememberMe = rememberMe;
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("./Login");
            }

            await SendTwoFactorCodeAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            ReturnUrl = returnUrl ?? Url.Content("~/");
            RememberMe = rememberMe;

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Unable to load two-factor authentication user.");
                return RedirectToPage("./Login");
            }

            var code = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
            var provider = ResolveTwoFactorProvider(user);

            if (_environment.IsDevelopment() && string.Equals(code, DevelopmentBypassCode, StringComparison.Ordinal))
            {
                await _signInManager.SignInAsync(user, rememberMe);
                await UpdateTwoFactorUsageAsync(user);
                await _loginEventLogger.LogAsync(user, LoginEventType.TwoFactorSuccess, true, RequestMetadataHelper.GetClientIp(HttpContext), RequestMetadataHelper.GetUserAgent(HttpContext), "Development bypass code.", HttpContext.RequestAborted);
                return LocalRedirect(ReturnUrl ?? Url.Content("~/")!);
            }

            var result = await _signInManager.TwoFactorSignInAsync(provider, code, rememberMe, Input.RememberMachine);

            if (result.Succeeded)
            {
                await UpdateTwoFactorUsageAsync(user);
                await _loginEventLogger.LogAsync(user, LoginEventType.TwoFactorSuccess, true, RequestMetadataHelper.GetClientIp(HttpContext), RequestMetadataHelper.GetUserAgent(HttpContext), cancellationToken: HttpContext.RequestAborted);
                _logger.LogInformation("User logged in with 2fa.");
                return LocalRedirect(ReturnUrl ?? Url.Content("~/")!);
            }

            if (result.IsLockedOut)
            {
                await _loginEventLogger.LogAsync(user, LoginEventType.AccountLockedOut, false, RequestMetadataHelper.GetClientIp(HttpContext), RequestMetadataHelper.GetUserAgent(HttpContext), "Locked during 2FA verification.", HttpContext.RequestAborted);
                _logger.LogWarning("User account locked out during 2fa verification.");
                return RedirectToPage("./Lockout");
            }

            await _loginEventLogger.LogAsync(user, LoginEventType.TwoFactorChallenge, false, RequestMetadataHelper.GetClientIp(HttpContext), RequestMetadataHelper.GetUserAgent(HttpContext), "Invalid two-factor code.", HttpContext.RequestAborted);
            ModelState.AddModelError(string.Empty, "Invalid authentication code.");
            return Page();
        }

        private async Task SendTwoFactorCodeAsync(ApplicationUser user)
        {
            var provider = ResolveTwoFactorProvider(user);
            if (provider == TokenOptions.DefaultEmailProvider && !string.IsNullOrWhiteSpace(user.Email))
            {
                var code = await _userManager.GenerateTwoFactorTokenAsync(user, provider);
                await _emailSender.SendEmailAsync(user.Email, "Your security code", $"Your security code is {code}");
                if (user.TwoFactorConfiguredAt is null)
                {
                    user.TwoFactorConfiguredAt = _timeProvider.GetUtcNow();
                    await _userManager.UpdateAsync(user);
                }
            }
        }

        private async Task UpdateTwoFactorUsageAsync(ApplicationUser user)
        {
            user.TwoFactorLastUsedAt = _timeProvider.GetUtcNow();
            await _userManager.UpdateAsync(user);
        }

        private string ResolveTwoFactorProvider(ApplicationUser user)
        {
            return user.TwoFactorMethod switch
            {
                TwoFactorMethod.AuthenticatorApp => TokenOptions.DefaultAuthenticatorProvider,
                TwoFactorMethod.Sms => TokenOptions.DefaultPhoneProvider,
                _ => TokenOptions.DefaultEmailProvider
            };
        }
    }
}
