using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SD.ProjectName.WebApp.Identity;

public sealed class DistributedSessionTicketStore : ITicketStore
{
    private const string CacheKeyPrefix = "auth-session-";

    private readonly IDistributedCache _cache;
    private readonly IOptionsMonitor<CookieAuthenticationOptions> _options;
    private readonly ILogger<DistributedSessionTicketStore> _logger;

    public DistributedSessionTicketStore(
        IDistributedCache cache,
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILogger<DistributedSessionTicketStore> logger)
    {
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = GenerateSessionKey();
        await RenewAsync(key, ticket);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = _options.Get(IdentityConstants.ApplicationScheme);
        var expiresUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.Add(options.ExpireTimeSpan);
        var lifetime = expiresUtc - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero)
        {
            lifetime = TimeSpan.FromMinutes(1);
        }

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime
        };

        if (options.SlidingExpiration)
        {
            cacheOptions.SlidingExpiration = options.ExpireTimeSpan;
        }

        var serializedTicket = TicketSerializer.Default.Serialize(ticket);
        return _cache.SetAsync(BuildKey(key), serializedTicket, cacheOptions);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var bytes = await _cache.GetAsync(BuildKey(key));
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return TicketSerializer.Default.Deserialize(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize authentication ticket for key {SessionKey}", key);
            await RemoveAsync(key);
            return null;
        }
    }

    public Task RemoveAsync(string key)
    {
        return _cache.RemoveAsync(BuildKey(key));
    }

    private static string BuildKey(string key) => CacheKeyPrefix + key;

    private static string GenerateSessionKey()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }
}
