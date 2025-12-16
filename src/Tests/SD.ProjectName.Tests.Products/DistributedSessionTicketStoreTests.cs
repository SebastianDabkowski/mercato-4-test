using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
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
        var clock = new TestTimeProvider();
        var cache = new TestDistributedCache(clock);
        var store = CreateStore(timeProvider: clock, cache: cache);
        var ticket = CreateTicket(TimeSpan.FromMinutes(1), clock);

        var sessionKey = await store.StoreAsync(ticket);
        Assert.NotNull(await store.RetrieveAsync(sessionKey));

        await store.RemoveAsync(sessionKey);

        var retrieved = await store.RetrieveAsync(sessionKey);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ExpiresTicket_WhenLifetimeExceeded()
    {
        var clock = new TestTimeProvider();
        var cache = new TestDistributedCache(clock);
        var store = CreateStore(TimeSpan.FromMinutes(1), cache: cache, timeProvider: clock);
        var ticket = CreateTicket(TimeSpan.FromMinutes(1), clock);

        var sessionKey = await store.StoreAsync(ticket);

        Assert.NotNull(await store.RetrieveAsync(sessionKey));

        clock.Advance(TimeSpan.FromMinutes(2));

        var expired = await store.RetrieveAsync(sessionKey);
        Assert.Null(expired);
    }

    private static DistributedSessionTicketStore CreateStore(
        TimeSpan? expiration = null,
        bool sliding = true,
        IDistributedCache? cache = null,
        TimeProvider? timeProvider = null)
    {
        var options = new CookieAuthenticationOptions
        {
            ExpireTimeSpan = expiration ?? TimeSpan.FromSeconds(1),
            SlidingExpiration = sliding,
            Cookie = { Name = "__Host-test" }
        };

        var monitor = new StaticOptionsMonitor<CookieAuthenticationOptions>(options);
        cache ??= new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        return new DistributedSessionTicketStore(
            cache,
            monitor,
            NullLogger<DistributedSessionTicketStore>.Instance,
            IdentityConstants.ApplicationScheme,
            timeProvider ?? TimeProvider.System);
    }

    private static AuthenticationTicket CreateTicket(TimeSpan? lifetime = null, TestTimeProvider? clock = null)
    {
        var now = clock?.GetUtcNow() ?? DateTimeOffset.UtcNow;
        var expires = now.Add(lifetime ?? TimeSpan.FromSeconds(1));
        var properties = new AuthenticationProperties
        {
            IssuedUtc = now,
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

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public TestTimeProvider()
            : this(DateTimeOffset.UtcNow)
        {
        }

        public TestTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoopTimer();

        public void Advance(TimeSpan value) => _utcNow = _utcNow.Add(value);

        private sealed class NoopTimer : ITimer
        {
            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        }
    }

    private sealed class TestDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, (byte[] Value, DateTimeOffset? Absolute, TimeSpan? Sliding)> _store = new();
        private readonly TestTimeProvider _clock;

        public TestDistributedCache(TestTimeProvider clock)
        {
            _clock = clock;
        }

        public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();

        public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!_store.TryGetValue(key, out var entry))
            {
                return Task.FromResult<byte[]?>(null);
            }

            if (entry.Absolute.HasValue && entry.Absolute.Value <= _clock.GetUtcNow())
            {
                _store.Remove(key);
                return Task.FromResult<byte[]?>(null);
            }

            if (entry.Sliding.HasValue)
            {
                _store[key] = (entry.Value, _clock.GetUtcNow().Add(entry.Sliding.Value), entry.Sliding);
            }

            return Task.FromResult<byte[]?>(entry.Value);
        }

        public void Refresh(string key) => RefreshAsync(key).GetAwaiter().GetResult();

        public Task RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_store.TryGetValue(key, out var entry) && entry.Sliding.HasValue)
            {
                _store[key] = (entry.Value, _clock.GetUtcNow().Add(entry.Sliding.Value), entry.Sliding);
            }

            return Task.CompletedTask;
        }

        public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            SetAsync(key, value, options).GetAwaiter().GetResult();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
        {
            var absoluteExpiration = options.AbsoluteExpiration ??
                                     (options.AbsoluteExpirationRelativeToNow.HasValue
                                         ? _clock.GetUtcNow().Add(options.AbsoluteExpirationRelativeToNow.Value)
                                         : null);

            _store[key] = (value, absoluteExpiration, options.SlidingExpiration);
            return Task.CompletedTask;
        }
    }
}
