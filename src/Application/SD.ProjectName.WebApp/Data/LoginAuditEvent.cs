using System.ComponentModel.DataAnnotations;

namespace SD.ProjectName.WebApp.Data
{
    public enum LoginEventType
    {
        PasswordSignInSuccess = 1,
        PasswordSignInFailure = 2,
        TwoFactorChallenge = 3,
        TwoFactorSuccess = 4,
        AccountLockedOut = 5
    }

    public class LoginAuditEvent
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(450)]
        public required string UserId { get; set; }

        [Required]
        public LoginEventType EventType { get; set; }

        public bool Succeeded { get; set; }

        public bool IsUnusual { get; set; }

        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [MaxLength(256)]
        public string? UserAgent { get; set; }

        [MaxLength(256)]
        public string? Reason { get; set; }

        [Required]
        public DateTimeOffset OccurredAt { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
