using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SD.ProjectName.Modules.Products.Infrastructure
{
    public class ProductDbContext : DbContext
    {
        public ProductDbContext(DbContextOptions<ProductDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProductModel> Products { get; set; }
        public DbSet<CategoryModel> Categories { get; set; }
        public DbSet<ProductImportJob> ImportJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductModel>().ToTable("ProductModel");
            modelBuilder.Entity<ProductModel>(entity =>
            {
                entity.Property(p => p.Sku)
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<CategoryModel>(entity =>
            {
                entity.ToTable("Category");
                entity.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(c => c.NormalizedName)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.HasIndex(c => c.NormalizedName)
                    .IsUnique();
                entity.Property(c => c.IsActive)
                    .HasDefaultValue(true);
                entity.Property(c => c.DisplayOrder)
                    .HasDefaultValue(0);
                entity.HasOne(c => c.Parent)
                    .WithMany(c => c.Children)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ProductImportJob>(entity =>
            {
                entity.ToTable("ProductImportJob");
                entity.Property(p => p.Status)
                    .HasMaxLength(50);
                entity.Property(p => p.FileName)
                    .HasMaxLength(255);
                entity.HasIndex(p => p.SellerId);
            });
        }

    }
}
