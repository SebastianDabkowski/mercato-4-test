using System.Collections.Generic;
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
    private static readonly TimeSpan MinimumLifetime = TimeSpan.FromMinutes(1);

    private readonly IDistributedCache _cache;
    private readonly IOptionsMonitor<CookieAuthenticationOptions> _options;
    private readonly ILogger<DistributedSessionTicketStore> _logger;
    private readonly string _schemeName;
    private readonly TimeProvider _timeProvider;

    public DistributedSessionTicketStore(
        IDistributedCache cache,
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILogger<DistributedSessionTicketStore> logger,
        string schemeName,
        TimeProvider? timeProvider = null)
    {
        _cache = cache;
        _options = options;
        _logger = logger;
        _schemeName = schemeName;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = GenerateSessionKey();
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = _options.Get(_schemeName);
        var now = _timeProvider.GetUtcNow();
        var expiresUtc = ticket.Properties.ExpiresUtc ?? now.Add(options.ExpireTimeSpan);
        var lifetime = expiresUtc - now;
        var ticketToStore = ticket;
        if (lifetime <= TimeSpan.Zero)
        {
            lifetime = MinimumLifetime;
            expiresUtc = now.Add(lifetime);

            var clonedProperties = CloneProperties(ticket.Properties);
            clonedProperties.ExpiresUtc = expiresUtc;
            clonedProperties.IssuedUtc ??= now;
            ticketToStore = new AuthenticationTicket(ticket.Principal, clonedProperties, ticket.AuthenticationScheme);
        }

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime
        };

        if (options.SlidingExpiration)
        {
            cacheOptions.SlidingExpiration = options.ExpireTimeSpan;
        }

        var serializedTicket = TicketSerializer.Default.Serialize(ticketToStore);
        try
        {
            await _cache.SetAsync(BuildKey(key), serializedTicket, cacheOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist authentication ticket for session {SessionKey}", key);
            throw;
        }
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
            _logger.LogWarning(ex, "Failed to deserialize authentication ticket for key {SessionKey}. Removing corrupted session entry.", key);
            await RemoveAsync(key);
            return null;
        }
    }

    public Task RemoveAsync(string key)
    {
        return _cache.RemoveAsync(BuildKey(key));
    }

    private static AuthenticationProperties CloneProperties(AuthenticationProperties properties)
    {
        var itemsCopy = new Dictionary<string, string?>(properties.Items);
        return new AuthenticationProperties(itemsCopy)
        {
            AllowRefresh = properties.AllowRefresh,
            IssuedUtc = properties.IssuedUtc,
            ExpiresUtc = properties.ExpiresUtc,
            IsPersistent = properties.IsPersistent,
            RedirectUri = properties.RedirectUri
        };
    }

    private static string BuildKey(string key) => CacheKeyPrefix + key;

    private static string GenerateSessionKey()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }
}
