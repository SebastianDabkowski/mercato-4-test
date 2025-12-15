using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ExternalLoginModel> _logger;

    public ExternalLoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<ExternalLoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [TempData]
    public string? ErrorMessage { get; set; }

    public string? ReturnUrl { get; private set; }

    public IActionResult OnPost(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl!);

        var cookieKey = $"fake-{provider.ToLowerInvariant()}-email";
        if (Request.Cookies.TryGetValue(cookieKey, out var fakeEmail) && !string.IsNullOrWhiteSpace(fakeEmail))
        {
            properties.Items["fake_email"] = fakeEmail;
        }

        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            ErrorMessage = $"Social login was canceled or failed: {HtmlEncoder.Default.Encode(remoteError)}";
            return RedirectToPage("./Login", new { ReturnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            ErrorMessage = "Error loading external login information. Please try again.";
            return RedirectToPage("./Login", new { ReturnUrl });
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.Succeeded)
        {
            var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existingUser is null)
            {
                ErrorMessage = "Unable to complete social login because the linked account was not found.";
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                return RedirectToPage("./Login", new { ReturnUrl });
            }

            if (existingUser.AccountType != AccountType.Buyer)
            {
                ErrorMessage = "Social login is only available for buyers.";
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                return RedirectToPage("./Login", new { ReturnUrl });
            }

            var redirect = await ResolveRedirectAsync(existingUser, NormalizeReturnUrl(ReturnUrl));
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return LocalRedirect(redirect);
        }

        if (signInResult.IsLockedOut)
        {
            ErrorMessage = "This account is locked because of too many failed attempts. Try again later.";
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToPage("./Login", new { ReturnUrl });
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorMessage = "Email information was not returned by the provider. Use email/password instead.";
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToPage("./Login", new { ReturnUrl });
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null && user.AccountType != AccountType.Buyer)
        {
            ErrorMessage = "Social login is only available for buyers.";
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return RedirectToPage("./Login", new { ReturnUrl });
        }

        if (user is null)
        {
            user = await CreateBuyerAsync(email, info);
            if (user is null)
            {
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                return RedirectToPage("./Login", new { ReturnUrl });
            }
        }
        else
        {
            var linked = await EnsureBuyerLoginAsync(user, info);
            if (!linked)
            {
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                return RedirectToPage("./Login", new { ReturnUrl });
            }
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        var redirectUrl = await ResolveRedirectAsync(user, NormalizeReturnUrl(ReturnUrl));
        return LocalRedirect(redirectUrl);
    }

    private async Task<ApplicationUser?> CreateBuyerAsync(string email, ExternalLoginInfo info)
    {
        var (firstName, lastName) = ResolveNames(email, info);
        var user = new ApplicationUser
        {
            AccountType = AccountType.Buyer,
            AccountStatus = AccountStatus.Verified,
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = lastName,
            RequiresKyc = false,
            KycStatus = KycStatus.Approved,
            TermsAcceptedAt = DateTimeOffset.UtcNow,
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            EmailVerificationSentAt = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            ErrorMessage = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return null;
        }

        user.EmailConfirmed = true;
        user.AccountStatus = AccountStatus.Verified;
        await _userManager.UpdateAsync(user);

        var roleResult = await _userManager.AddToRoleAsync(user, IdentityRoles.Buyer);
        if (!roleResult.Succeeded)
        {
            ErrorMessage = string.Join(" ", roleResult.Errors.Select(e => e.Description));
            return null;
        }

        var addLoginResult = await _userManager.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded)
        {
            ErrorMessage = string.Join(" ", addLoginResult.Errors.Select(e => e.Description));
            return null;
        }

        _logger.LogInformation("Created new buyer account via {Provider} for {Email}.", info.LoginProvider, email);
        return user;
    }

    private async Task<bool> EnsureBuyerLoginAsync(ApplicationUser user, ExternalLoginInfo info)
    {
        user.EmailConfirmed = true;
        user.EmailVerifiedAt ??= DateTimeOffset.UtcNow;
        user.AccountStatus = AccountStatus.Verified;
        await _userManager.UpdateAsync(user);

        var existingLogin = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (existingLogin is null)
        {
            var linkResult = await _userManager.AddLoginAsync(user, info);
            if (!linkResult.Succeeded)
            {
                ErrorMessage = string.Join(" ", linkResult.Errors.Select(e => e.Description));
                return false;
            }
        }

        return true;
    }

    private async Task<string> ResolveRedirectAsync(ApplicationUser user, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        if (user.AccountType == AccountType.Buyer)
        {
            return Url.Content("~/buyer/dashboard")!;
        }

        if (user.AccountType == AccountType.Seller)
        {
            if (user.RequiresKyc && user.KycStatus != KycStatus.Approved)
            {
                return Url.Content("~/seller/kyc")!;
            }

            return Url.Content("~/seller/dashboard")!;
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(IdentityRoles.Buyer))
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

        return Url.Content("~/")!;
    }

    private (string firstName, string lastName) ResolveNames(string email, ExternalLoginInfo info)
    {
        var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName);
        var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);

        if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
        {
            return (firstName, lastName);
        }

        var localPart = email.Split('@')[0];
        var nameSegments = localPart.Split('.', '-', '_');
        var resolvedFirst = firstName ?? (nameSegments.FirstOrDefault() ?? "Buyer");
        var resolvedLast = lastName ?? (nameSegments.Skip(1).FirstOrDefault() ?? "User");
        return (Capitalize(resolvedFirst), Capitalize(resolvedLast));
    }

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Buyer" : char.ToUpperInvariant(value[0]) + value[1..];

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
