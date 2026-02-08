using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SD.ProjectName.WebApp.Services;

public class CartIdentityService : ICartIdentityService
{
    private const string GuestCartCookieName = "guest_cart_id";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;

    public CartIdentityService(IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
    }

    public string GetOrCreateBuyerId()
    {
        var httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HTTP context.");
        var userBuyerId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userBuyerId))
        {
            return userBuyerId!;
        }

        var guestBuyerId = GetGuestBuyerId();
        if (!string.IsNullOrWhiteSpace(guestBuyerId))
        {
            return guestBuyerId!;
        }

        var newGuestBuyerId = $"guest-{Guid.NewGuid():N}";
        httpContext.Response.Cookies.Append(GuestCartCookieName, newGuestBuyerId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = _timeProvider.GetUtcNow().AddDays(30)
        });

        return newGuestBuyerId;
    }

    public string? GetGuestBuyerId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var fromCookie = httpContext?.Request.Cookies[GuestCartCookieName];

        return string.IsNullOrWhiteSpace(fromCookie) ? null : fromCookie;
    }

    public void ClearGuestBuyerId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        httpContext?.Response.Cookies.Delete(GuestCartCookieName);
    }
}
