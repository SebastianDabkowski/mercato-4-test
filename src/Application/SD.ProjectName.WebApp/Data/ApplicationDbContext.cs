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
                    .HasDefaultValue(SellerType.Individual);

                entity.Property(u => u.KycStatus)
                    .HasConversion<string>()
                    .HasMaxLength(32)
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
