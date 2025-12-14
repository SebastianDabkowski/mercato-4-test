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
    }
}
