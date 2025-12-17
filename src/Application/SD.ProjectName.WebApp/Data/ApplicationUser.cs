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

    public enum SellerType
    {
        Individual = 1,
        Company = 2
    }

    public enum KycStatus
    {
        NotStarted = 1,
        Pending = 2,
        Approved = 3,
        Rejected = 4
    }

    public enum TwoFactorMethod
    {
        None = 0,
        EmailCode = 1,
        AuthenticatorApp = 2,
        Sms = 3
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

        public SellerType SellerType { get; set; } = SellerType.Individual;

        [PersonalData]
        [MaxLength(100)]
        public string? VerificationRegistrationNumber { get; set; }

        [PersonalData]
        [MaxLength(300)]
        public string? VerificationAddress { get; set; }

        [PersonalData]
        [MaxLength(150)]
        public string? VerificationContactPerson { get; set; }

        [PersonalData]
        [MaxLength(100)]
        public string? VerificationPersonalIdNumber { get; set; }

        [PersonalData]
        [MaxLength(120)]
        public string? StoreName { get; set; }

        [PersonalData]
        [MaxLength(1000)]
        public string? StoreDescription { get; set; }

        [PersonalData]
        [MaxLength(320)]
        public string? StoreContactEmail { get; set; }

        [PersonalData]
        [MaxLength(64)]
        public string? StoreContactPhone { get; set; }

        [PersonalData]
        [MaxLength(2048)]
        public string? StoreWebsiteUrl { get; set; }

        [PersonalData]
        [MaxLength(260)]
        public string? StoreLogoPath { get; set; }

        public AccountType AccountType { get; set; }

        public AccountStatus AccountStatus { get; set; } = AccountStatus.Unverified;

        public DateTimeOffset TermsAcceptedAt { get; set; }

        public DateTimeOffset? EmailVerificationSentAt { get; set; }

        public DateTimeOffset? EmailVerifiedAt { get; set; }

        public bool RequiresKyc { get; set; }

        public KycStatus KycStatus { get; set; } = KycStatus.NotStarted;

        public DateTimeOffset? KycSubmittedAt { get; set; }

        public DateTimeOffset? KycApprovedAt { get; set; }

        [PersonalData]
        public TwoFactorMethod TwoFactorMethod { get; set; } = TwoFactorMethod.None;

        [PersonalData]
        public DateTimeOffset? TwoFactorConfiguredAt { get; set; }

        public DateTimeOffset? TwoFactorLastUsedAt { get; set; }

        public DateTimeOffset? TwoFactorRecoveryCodesGeneratedAt { get; set; }
    }
}
