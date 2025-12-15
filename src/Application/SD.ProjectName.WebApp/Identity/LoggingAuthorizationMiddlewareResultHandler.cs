using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace SD.ProjectName.WebApp.Identity
{
    public class LoggingAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
        private readonly ILogger<LoggingAuthorizationMiddlewareResultHandler> _logger;

        public LoggingAuthorizationMiddlewareResultHandler(ILogger<LoggingAuthorizationMiddlewareResultHandler> logger)
        {
            _logger = logger;
        }

        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Forbidden || authorizeResult.Challenged)
            {
                var userName = context.User?.Identity?.IsAuthenticated == true
                    ? context.User.Identity?.Name ?? "unknown"
                    : "anonymous";

                var roles = context.User?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
                var requirements = policy.Requirements.Select(r => r.ToString()).ToArray();

                _logger.LogWarning("Authorization {Outcome} for {User} on {Path}. Roles: {Roles}. Requirements: {Requirements}",
                    authorizeResult.Forbidden ? "failure" : "challenge",
                    userName,
                    context.Request.Path,
                    roles.Length == 0 ? "none" : string.Join(", ", roles),
                    requirements.Length == 0 ? "none" : string.Join(", ", requirements));
            }

            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
