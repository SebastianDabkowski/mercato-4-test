using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.WebApp.Identity
{
    public class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
    {
        private static readonly HashSet<string> CommonPasswords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "123456",
            "123456789",
            "qwerty",
            "111111",
            "abc123",
            "password1",
            "123123",
            "letmein",
            "welcome"
        };

        private readonly ILogger<CommonPasswordValidator> _logger;

        public CommonPasswordValidator(ILogger<CommonPasswordValidator> logger)
        {
            _logger = logger;
        }

        public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
        {
            if (password is null)
            {
                return Task.FromResult(IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordRequired",
                    Description = "Password is required."
                }));
            }

            if (CommonPasswords.Contains(password))
            {
                _logger.LogWarning("Rejected common password for user {Email}", user.Email);
                return Task.FromResult(IdentityResult.Failed(new IdentityError
                {
                    Code = "CommonPassword",
                    Description = "Choose a password that is not commonly used."
                }));
            }

            return Task.FromResult(IdentityResult.Success);
        }
    }
}
