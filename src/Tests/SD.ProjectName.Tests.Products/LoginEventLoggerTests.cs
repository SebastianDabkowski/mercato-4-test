using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.Tests.Products
{
    public class LoginEventLoggerTests
    {
        [Fact]
        public async Task LogAsync_MarksUnusualWhenIpChanges()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            using var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();

            var logger = new LoginEventLogger(context, TimeProvider.System, NullLogger<LoginEventLogger>.Instance);
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            await logger.LogAsync(user, LoginEventType.PasswordSignInSuccess, true, "1.1.1.1", "agent");
            var second = await logger.LogAsync(user, LoginEventType.PasswordSignInSuccess, true, "2.2.2.2", "agent");

            Assert.True(second.IsUnusual);
        }

        [Fact]
        public async Task LogAsync_PrunesExpiredEvents()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            using var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();

            var logger = new LoginEventLogger(context, TimeProvider.System, NullLogger<LoginEventLogger>.Instance);
            var user = CreateUser();
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var now = TimeProvider.System.GetUtcNow();
            var expired = new LoginAuditEvent
            {
                UserId = user.Id,
                EventType = LoginEventType.PasswordSignInSuccess,
                Succeeded = true,
                OccurredAt = now.AddDays(-(LoginEventLogger.RetentionDays + 1)),
                ExpiresAt = now.AddDays(-1)
            };

            context.LoginAuditEvents.Add(expired);
            await context.SaveChangesAsync();

            await logger.LogAsync(user, LoginEventType.PasswordSignInSuccess, true, "1.1.1.1", "agent");

            Assert.Null(await context.LoginAuditEvents.FindAsync(expired.Id));
        }

        private static ApplicationUser CreateUser()
        {
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "user@example.com",
                UserName = "user@example.com",
                NormalizedEmail = "USER@EXAMPLE.COM",
                NormalizedUserName = "USER@EXAMPLE.COM",
                FirstName = "Test",
                LastName = "User",
                AccountType = AccountType.Buyer,
                TermsAcceptedAt = DateTimeOffset.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };
        }
    }
}
