using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.Tests.Products
{
    public class AuthorizationLoggingTests
    {
        [Fact]
        public async Task LoggingAuthorizationMiddlewareResultHandler_LogsForbiddenAttempt()
        {
            var logger = new TestLogger<LoggingAuthorizationMiddlewareResultHandler>();
            var handler = new LoggingAuthorizationMiddlewareResultHandler(logger);
            var context = new DefaultHttpContext();
            context.Request.Path = "/seller/dashboard";
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "user@example.com"),
                new Claim(ClaimTypes.Role, IdentityRoles.Buyer)
            }, "Test"));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            var provider = services.BuildServiceProvider();
            context.RequestServices = provider;

            var policy = new AuthorizationPolicyBuilder().RequireRole(IdentityRoles.Seller).Build();
            var result = PolicyAuthorizationResult.Forbid();

            await handler.HandleAsync(_ => Task.CompletedTask, context, policy, result);

            Assert.Contains(logger.Messages, m => m.Contains("Authorization failure") && m.Contains("/seller/dashboard"));
        }

        [Fact]
        public void IdentityRoles_ShouldIncludeAdminForSeeding()
        {
            Assert.Contains(IdentityRoles.Admin, IdentityRoles.All);
        }

        private sealed class TestLogger<T> : ILogger<T>
        {
            public List<string> Messages { get; } = new();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Messages.Add($"{logLevel}: {formatter(state, exception)}");
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }

        private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
                : base(options, logger, encoder, clock)
            {
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity("Test"));
                var ticket = new AuthenticationTicket(principal, "Test");
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }
    }
}
