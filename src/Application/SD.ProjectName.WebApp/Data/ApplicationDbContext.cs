using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SD.ProjectName.WebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
        public DbSet<LoginAuditEvent> LoginAuditEvents { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(u => u.AccountStatus)
                    .HasConversion<string>()
                    .HasMaxLength(32);

                entity.Property(u => u.AccountType)
                    .HasConversion<string>()
                    .HasMaxLength(32);

                entity.Property(u => u.SellerType)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    // Keep explicit default to match existing schema defaults.
                    .HasDefaultValue(SellerType.Individual);

                entity.Property(u => u.KycStatus)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    // Keep explicit default to match existing schema defaults.
                    .HasDefaultValue(KycStatus.NotStarted);

                entity.Property(u => u.RequiresKyc)
                    .HasDefaultValue(false);

                entity.Property(u => u.VerificationRegistrationNumber)
                    .HasMaxLength(100);

                entity.Property(u => u.VerificationAddress)
                    .HasMaxLength(300);

                entity.Property(u => u.VerificationContactPerson)
                    .HasMaxLength(150);

                entity.Property(u => u.VerificationPersonalIdNumber)
                    .HasMaxLength(100);

                var storeNameProperty = entity.Property(u => u.StoreName)
                    .HasMaxLength(120);

                if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                {
                    storeNameProperty.UseCollation("NOCASE");
                }

                entity.HasIndex(u => u.StoreName)
                    .IsUnique()
                    .HasFilter("StoreName IS NOT NULL");

                entity.Property(u => u.StoreDescription)
                    .HasMaxLength(1000);

                entity.Property(u => u.StoreContactEmail)
                    .HasMaxLength(320);

                entity.Property(u => u.StoreContactPhone)
                    .HasMaxLength(64);

                entity.Property(u => u.StoreWebsiteUrl)
                    .HasMaxLength(2048);

                entity.Property(u => u.StoreLogoPath)
                    .HasMaxLength(260);

                entity.Property(u => u.FirstName)
                    .IsRequired();

                entity.Property(u => u.LastName)
                    .IsRequired();

                entity.Property(u => u.TermsAcceptedAt)
                    .IsRequired();

                entity.Property(u => u.TwoFactorMethod)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .HasDefaultValue(TwoFactorMethod.None);

                entity.Property(u => u.PayoutDefaultMethod)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .HasDefaultValue(PayoutMethod.BankTransfer);

                entity.Property(u => u.SellerRole)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .HasDefaultValue(SellerTeamRole.StoreOwner);

                entity.Property(u => u.StoreOwnerId)
                    .HasMaxLength(450);

                entity.HasIndex(u => u.StoreOwnerId);

                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(u => u.StoreOwnerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<LoginAuditEvent>(entity =>
            {
                entity.Property(e => e.EventType)
                    .HasConversion<string>()
                    .HasMaxLength(64);

                entity.Property(e => e.IpAddress)
                    .HasMaxLength(64);

                entity.Property(e => e.UserAgent)
                    .HasMaxLength(256);

                entity.Property(e => e.Reason)
                    .HasMaxLength(256);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.OccurredAt);
                entity.HasIndex(e => e.ExpiresAt);
            });
        }
    }
}
