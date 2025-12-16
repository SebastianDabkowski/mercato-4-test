using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.WebApp.Services
{
    public interface ILoginEventLogger
    {
        Task<LoginAuditEvent> LogAsync(
            ApplicationUser user,
            LoginEventType eventType,
            bool succeeded,
            string? ipAddress,
            string? userAgent,
            string? reason = null,
            CancellationToken cancellationToken = default);
    }

    public class LoginEventLogger : ILoginEventLogger
    {
        public const int RetentionDays = 180;

        private readonly ApplicationDbContext _dbContext;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<LoginEventLogger> _logger;

        public LoginEventLogger(ApplicationDbContext dbContext, TimeProvider timeProvider, ILogger<LoginEventLogger> logger)
        {
            _dbContext = dbContext;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<LoginAuditEvent> LogAsync(
            ApplicationUser user,
            LoginEventType eventType,
            bool succeeded,
            string? ipAddress,
            string? userAgent,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            await PruneExpiredAsync(cancellationToken);

            var now = _timeProvider.GetUtcNow();
            var isUnusual = await IsUnusualAsync(user.Id, eventType, succeeded, ipAddress, cancellationToken);

            var loginEvent = new LoginAuditEvent
            {
                UserId = user.Id,
                EventType = eventType,
                Succeeded = succeeded,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Reason = reason,
                OccurredAt = now,
                ExpiresAt = now.AddDays(RetentionDays),
                IsUnusual = isUnusual
            };

            _dbContext.LoginAuditEvents.Add(loginEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (isUnusual)
            {
                _logger.LogWarning(
                    "Unusual login activity detected for user {UserId}. Event {EventType} from IP {IpAddress}. Reason: {Reason}",
                    user.Id,
                    eventType,
                    ipAddress,
                    reason);
            }

            return loginEvent;
        }

        private async Task<bool> IsUnusualAsync(string userId, LoginEventType eventType, bool succeeded, string? ipAddress, CancellationToken cancellationToken)
        {
            if (eventType == LoginEventType.AccountLockedOut)
            {
                return true;
            }

            if (!succeeded || string.IsNullOrWhiteSpace(ipAddress))
            {
                return false;
            }

            var lastSuccess = await _dbContext.LoginAuditEvents
                .Where(e => e.UserId == userId && e.Succeeded)
                .OrderByDescending(e => e.Id)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (lastSuccess is null)
            {
                return false;
            }

            return !string.Equals(lastSuccess.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase);
        }

        private async Task PruneExpiredAsync(CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();
            var expiredIds = _dbContext.LoginAuditEvents
                .Select(e => new { e.Id, e.ExpiresAt })
                .AsEnumerable()
                .Where(e => e.ExpiresAt != null && e.ExpiresAt <= now)
                .Select(e => e.Id)
                .ToList();

            if (expiredIds.Count == 0)
            {
                return;
            }

            var expired = await _dbContext.LoginAuditEvents
                .Where(e => expiredIds.Contains(e.Id))
                .ToListAsync(cancellationToken);

            _dbContext.LoginAuditEvents.RemoveRange(expired);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
