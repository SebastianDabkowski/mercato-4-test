using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Modules.Cart.Infrastructure;

public class CartDbContext : DbContext
{
    public CartDbContext(DbContextOptions<CartDbContext> options)
        : base(options)
    {
    }

    public DbSet<CartModel> Carts { get; set; }
    public DbSet<CartItemModel> CartItems { get; set; }
    public DbSet<ShippingRuleModel> ShippingRules { get; set; }
    public DbSet<DeliveryAddressModel> DeliveryAddresses { get; set; }
    public DbSet<ShippingSelectionModel> ShippingSelections { get; set; }
    public DbSet<PaymentSelectionModel> PaymentSelections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CartModel>(entity =>
        {
            entity.ToTable("Cart");
            entity.HasIndex(c => c.UserId);
        });

        modelBuilder.Entity<CartItemModel>(entity =>
        {
            entity.ToTable("CartItem");
            entity.HasIndex(ci => ci.CartId);
            entity.HasIndex(ci => ci.BuyerId);
            entity.HasIndex(ci => ci.ProductId);
            entity.HasIndex(ci => ci.SellerId);
            entity.Property(ci => ci.ProductSku).HasMaxLength(100);
            entity.Property(ci => ci.ProductName).HasMaxLength(500);
        });

        modelBuilder.Entity<ShippingRuleModel>(entity =>
        {
            entity.ToTable("ShippingRule");
            entity.HasIndex(sr => sr.SellerId);
            entity.Property(sr => sr.ShippingMethod).HasMaxLength(100);
        });

        modelBuilder.Entity<DeliveryAddressModel>(entity =>
        {
            entity.ToTable("DeliveryAddress");
            entity.HasIndex(a => a.BuyerId);
            entity.HasIndex(a => a.IsSelectedForCheckout);
            entity.Property(a => a.RecipientName).HasMaxLength(200);
            entity.Property(a => a.Line1).HasMaxLength(300);
            entity.Property(a => a.Line2).HasMaxLength(300);
            entity.Property(a => a.City).HasMaxLength(150);
            entity.Property(a => a.Region).HasMaxLength(150);
            entity.Property(a => a.PostalCode).HasMaxLength(50);
            entity.Property(a => a.CountryCode).HasMaxLength(3);
            entity.Property(a => a.PhoneNumber).HasMaxLength(50);
        });

        modelBuilder.Entity<ShippingSelectionModel>(entity =>
        {
            entity.ToTable("ShippingSelection");
            entity.HasIndex(s => new { s.BuyerId, s.SellerId }).IsUnique();
            entity.Property(s => s.ShippingMethod).HasMaxLength(100);
        });

        modelBuilder.Entity<PaymentSelectionModel>(entity =>
        {
            entity.ToTable("PaymentSelection");
            entity.HasIndex(p => p.BuyerId).IsUnique();
            entity.Property(p => p.PaymentMethod).HasMaxLength(100);
            entity.Property(p => p.Status).HasConversion<int>();
        });
    }
}
