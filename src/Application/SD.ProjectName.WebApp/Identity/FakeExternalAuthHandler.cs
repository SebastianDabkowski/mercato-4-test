using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SD.ProjectName.WebApp.Identity;

public class FakeExternalAuthOptions : AuthenticationSchemeOptions
{
    public string? DefaultEmail { get; set; }
    public string? DefaultGivenName { get; set; }
    public string? DefaultSurname { get; set; }
}

public class FakeExternalAuthHandler : AuthenticationHandler<FakeExternalAuthOptions>
{
    public FakeExternalAuthHandler(IOptionsMonitor<FakeExternalAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var email = ResolveEmail(properties) ?? $"user-{Scheme.Name.ToLowerInvariant()}@example.com";
        var givenName = Options.DefaultGivenName ?? "Social";
        var surname = Options.DefaultSurname ?? "User";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, email, ClaimValueTypes.String, Scheme.Name),
            new(ClaimTypes.Email, email, ClaimValueTypes.String, Scheme.Name),
            new(ClaimTypes.Name, email, ClaimValueTypes.String, Scheme.Name),
            new(ClaimTypes.GivenName, givenName, ClaimValueTypes.String, Scheme.Name),
            new(ClaimTypes.Surname, surname, ClaimValueTypes.String, Scheme.Name)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        Context.SignInAsync(IdentityConstants.ExternalScheme, principal, properties);

        var redirectUri = properties.RedirectUri ?? "/";
        Context.Response.Redirect(redirectUri);
        return Task.CompletedTask;
    }

    private string? ResolveEmail(AuthenticationProperties properties)
    {
        if (properties.Items.TryGetValue("fake_email", out var emailFromProperties) && !string.IsNullOrWhiteSpace(emailFromProperties))
        {
            return emailFromProperties;
        }

        var cookieKey = $"fake-{Scheme.Name.ToLowerInvariant()}-email";
        if (Context.Request.Cookies.TryGetValue(cookieKey, out var emailFromCookie) && !string.IsNullOrWhiteSpace(emailFromCookie))
        {
            return emailFromCookie;
        }

        return Options.DefaultEmail;
    }
}
