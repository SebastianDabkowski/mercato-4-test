using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.Tests.Products;

public class DistributedSessionTicketStoreTests
{
    [Fact]
    public async Task RemovesTicket_WhenExplicitlyRemoved()
    {
        var store = CreateStore();
        var ticket = CreateTicket(TimeSpan.FromMinutes(1));

        var sessionKey = await store.StoreAsync(ticket);
        Assert.NotNull(await store.RetrieveAsync(sessionKey));

        await store.RemoveAsync(sessionKey);

        var retrieved = await store.RetrieveAsync(sessionKey);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ExpiresTicket_WhenLifetimeExceeded()
    {
        var store = CreateStore(TimeSpan.FromMilliseconds(200));
        var ticket = CreateTicket(TimeSpan.FromMilliseconds(200));

        var sessionKey = await store.StoreAsync(ticket);

        Assert.NotNull(await store.RetrieveAsync(sessionKey));

        await Task.Delay(500);

        var expired = await store.RetrieveAsync(sessionKey);
        Assert.Null(expired);
    }

    private static DistributedSessionTicketStore CreateStore(
        TimeSpan? expiration = null,
        bool sliding = true,
        IDistributedCache? cache = null)
    {
        var options = new CookieAuthenticationOptions
        {
            ExpireTimeSpan = expiration ?? TimeSpan.FromSeconds(1),
            SlidingExpiration = sliding,
            Cookie = { Name = "__Host-test" }
        };

        var monitor = new StaticOptionsMonitor<CookieAuthenticationOptions>(options);
        cache ??= new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        return new DistributedSessionTicketStore(cache, monitor, NullLogger<DistributedSessionTicketStore>.Instance);
    }

    private static AuthenticationTicket CreateTicket(TimeSpan? lifetime = null)
    {
        var expires = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromSeconds(1));
        var properties = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = expires
        };
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim(ClaimTypes.Email, "user@example.com")
        }, IdentityConstants.ApplicationScheme);

        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, properties, IdentityConstants.ApplicationScheme);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public StaticOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
