using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SD.ProjectName.WebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

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

                entity.Property(u => u.KycStatus)
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .HasDefaultValue(KycStatus.NotStarted);

                entity.Property(u => u.RequiresKyc)
                    .HasDefaultValue(false);

                entity.Property(u => u.FirstName)
                    .IsRequired();

                entity.Property(u => u.LastName)
                    .IsRequired();

                entity.Property(u => u.TermsAcceptedAt)
                    .IsRequired();
            });
        }
    }
}
