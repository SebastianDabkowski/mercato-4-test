using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SD.ProjectName.WebApp.Data
{
    public enum AccountType
    {
        Buyer = 1,
        Seller = 2
    }

    public enum AccountStatus
    {
        Unverified = 1,
        Verified = 2,
        Suspended = 3
    }

    public enum KycStatus
    {
        NotStarted = 1,
        Pending = 2,
        Approved = 3,
        Rejected = 4
    }

    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        [MaxLength(100)]
        public required string FirstName { get; set; }

        [PersonalData]
        [MaxLength(100)]
        public required string LastName { get; set; }

        [MaxLength(200)]
        public string? CompanyName { get; set; }

        [MaxLength(50)]
        public string? TaxId { get; set; }

        public AccountType AccountType { get; set; }

        public AccountStatus AccountStatus { get; set; } = AccountStatus.Unverified;

        public DateTimeOffset TermsAcceptedAt { get; set; }

        public DateTimeOffset? EmailVerificationSentAt { get; set; }

        public DateTimeOffset? EmailVerifiedAt { get; set; }

        public bool RequiresKyc { get; set; }

        public KycStatus KycStatus { get; set; } = KycStatus.NotStarted;

        public DateTimeOffset? KycSubmittedAt { get; set; }

        public DateTimeOffset? KycApprovedAt { get; set; }
    }
}
