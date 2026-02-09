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
    public DbSet<PromoCodeModel> PromoCodes { get; set; }
    public DbSet<PromoSelectionModel> PromoSelections { get; set; }
    public DbSet<OrderModel> Orders { get; set; }
    public DbSet<OrderItemModel> OrderItems { get; set; }
    public DbSet<OrderShippingSelectionModel> OrderShippingSelections { get; set; }
    public DbSet<SellerOrderModel> SellerOrders { get; set; }
    public DbSet<ReturnRequestModel> ReturnRequests { get; set; }
    public DbSet<ReturnRequestItemModel> ReturnRequestItems { get; set; }
    public DbSet<EscrowLedgerEntry> EscrowLedgerEntries { get; set; }
    public DbSet<PayoutSchedule> PayoutSchedules { get; set; }
    public DbSet<PayoutScheduleItem> PayoutScheduleItems { get; set; }
    public DbSet<CommissionInvoice> CommissionInvoices { get; set; }
    public DbSet<CommissionInvoiceLine> CommissionInvoiceLines { get; set; }

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
            entity.Property(ci => ci.Category).HasMaxLength(200);
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
            entity.HasIndex(p => p.ProviderReference).IsUnique();
            entity.Property(p => p.PaymentMethod).HasMaxLength(100);
            entity.Property(p => p.ProviderReference).HasMaxLength(200);
            entity.Property(p => p.Status).HasConversion<int>();
        });

        modelBuilder.Entity<PromoCodeModel>(entity =>
        {
            entity.ToTable("PromoCode");
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.Code).HasMaxLength(50);
            entity.Property(p => p.Description).HasMaxLength(500);
            entity.Property(p => p.SellerId).HasMaxLength(200);
            entity.Property(p => p.DiscountType).HasConversion<int>();
            entity.HasData(
                new PromoCodeModel
                {
                    Id = 1,
                    Code = "WELCOME10",
                    Description = "10% off any order over 50",
                    DiscountType = PromoDiscountType.Percentage,
                    DiscountValue = 0.10m,
                    MinimumSubtotal = 50m,
                    ValidFrom = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    ValidUntil = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    IsActive = true
                },
                new PromoCodeModel
                {
                    Id = 2,
                    Code = "SELLER5",
                    Description = "5 currency units off Seller One items over 20",
                    DiscountType = PromoDiscountType.FixedAmount,
                    DiscountValue = 5m,
                    SellerId = "seller-1",
                    MinimumSubtotal = 20m,
                    ValidFrom = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    ValidUntil = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    IsActive = true
                });
        });

        modelBuilder.Entity<PromoSelectionModel>(entity =>
        {
            entity.ToTable("PromoSelection");
            entity.HasIndex(p => p.BuyerId).IsUnique();
            entity.Property(p => p.PromoCode).HasMaxLength(50);
        });

        modelBuilder.Entity<OrderModel>(entity =>
        {
            entity.ToTable("Order");
            entity.HasIndex(o => o.BuyerId);
            entity.HasIndex(o => o.CreatedAt);
            entity.HasIndex(o => o.Status);
            entity.Property(o => o.PaymentMethod).HasMaxLength(100);
            entity.Property(o => o.Status).HasMaxLength(50);
            entity.Property(o => o.RefundedAmount);
            entity.Property(o => o.DeliveryRecipientName).HasMaxLength(200);
            entity.Property(o => o.DeliveryLine1).HasMaxLength(300);
            entity.Property(o => o.DeliveryLine2).HasMaxLength(300);
            entity.Property(o => o.DeliveryCity).HasMaxLength(150);
            entity.Property(o => o.DeliveryRegion).HasMaxLength(150);
            entity.Property(o => o.DeliveryPostalCode).HasMaxLength(50);
            entity.Property(o => o.DeliveryCountryCode).HasMaxLength(3);
            entity.Property(o => o.DeliveryPhoneNumber).HasMaxLength(50);
            entity.Property(o => o.PromoCode).HasMaxLength(50);
            entity.Property(o => o.CommissionTotal).HasColumnType("decimal(18,6)");
        });

        modelBuilder.Entity<OrderItemModel>(entity =>
        {
            entity.ToTable("OrderItem");
            entity.HasIndex(oi => oi.OrderId);
            entity.HasIndex(oi => oi.SellerOrderId);
            entity.Property(oi => oi.ProductSku).HasMaxLength(100);
            entity.Property(oi => oi.ProductName).HasMaxLength(500);
            entity.Property(oi => oi.Category).HasMaxLength(200);
            entity.Property(oi => oi.SellerId).HasMaxLength(100);
            entity.Property(oi => oi.SellerName).HasMaxLength(200);
            entity.Property(oi => oi.Status).HasMaxLength(50).HasDefaultValue(OrderStatus.Preparing);
            entity
                .HasOne<OrderModel>()
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(oi => oi.SellerOrder)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.SellerOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderShippingSelectionModel>(entity =>
        {
            entity.ToTable("OrderShippingSelection");
            entity.HasIndex(os => os.OrderId);
            entity.HasIndex(os => os.SellerOrderId).IsUnique();
            entity.Property(os => os.SellerId).HasMaxLength(100);
            entity.Property(os => os.SellerName).HasMaxLength(200);
            entity.Property(os => os.ShippingMethod).HasMaxLength(100);
            entity
                .HasOne<OrderModel>()
                .WithMany(o => o.ShippingSelections)
                .HasForeignKey(os => os.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(os => os.SellerOrder)
                .WithOne(o => o.ShippingSelection)
                .HasForeignKey<OrderShippingSelectionModel>(os => os.SellerOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerOrderModel>(entity =>
        {
            entity.ToTable("SellerOrder");
            entity.HasIndex(o => o.OrderId);
            entity.HasIndex(o => o.SellerId);
            entity.Property(o => o.SellerId).HasMaxLength(100);
            entity.Property(o => o.SellerName).HasMaxLength(200);
            entity.Property(o => o.Status).HasMaxLength(50);
            entity.Property(o => o.TrackingNumber).HasMaxLength(200);
            entity.Property(o => o.RefundedAmount);
            entity.Property(o => o.DeliveredAt);
            entity.Property(o => o.CommissionRateApplied).HasColumnType("decimal(9,6)");
            entity.Property(o => o.CommissionAmount).HasColumnType("decimal(18,6)");
            entity.Property(o => o.CommissionCalculatedAt);
            entity
                .HasMany(o => o.ReturnRequests)
                .WithOne(r => r.SellerOrder)
                .HasForeignKey(r => r.SellerOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(o => o.Order)
                .WithMany(o => o.SubOrders)
                .HasForeignKey(o => o.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReturnRequestModel>(entity =>
        {
            entity.ToTable("ReturnRequest");
            entity.HasIndex(r => r.OrderId);
            entity.HasIndex(r => r.SellerOrderId);
            entity.HasIndex(r => r.Status);
            entity.Property(r => r.BuyerId).HasMaxLength(200);
            entity.Property(r => r.Status).HasMaxLength(50);
            entity.Property(r => r.Reason).HasMaxLength(2000);
            entity.Property(r => r.RequestedAt);
            entity.Property(r => r.UpdatedAt);
            entity
                .HasOne(r => r.Order)
                .WithMany()
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReturnRequestItemModel>(entity =>
        {
            entity.ToTable("ReturnRequestItem");
            entity.HasIndex(i => i.ReturnRequestId);
            entity.Property(i => i.Quantity);
            entity
                .HasOne(i => i.ReturnRequest)
                .WithMany(r => r.Items)
                .HasForeignKey(i => i.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(i => i.OrderItem)
                .WithMany()
                .HasForeignKey(i => i.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EscrowLedgerEntry>(entity =>
        {
            entity.ToTable("EscrowLedger");
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.SellerOrderId).IsUnique();
            entity.Property(e => e.BuyerId).HasMaxLength(200);
            entity.Property(e => e.SellerId).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.ReleaseReason).HasMaxLength(500);
            entity.Property(e => e.HeldAmount).HasColumnType("decimal(18,6)");
            entity.Property(e => e.CommissionAmount).HasColumnType("decimal(18,6)");
            entity.Property(e => e.SellerPayoutAmount).HasColumnType("decimal(18,6)");
        });

        modelBuilder.Entity<PayoutSchedule>(entity =>
        {
            entity.ToTable("PayoutSchedule");
            entity.HasIndex(p => p.SellerId);
            entity.Property(p => p.SellerId).HasMaxLength(200);
            entity.Property(p => p.Status).HasMaxLength(50);
            entity.Property(p => p.ErrorReference).HasMaxLength(500);
            entity.Property(p => p.TotalAmount).HasColumnType("decimal(18,6)");
        });

        modelBuilder.Entity<PayoutScheduleItem>(entity =>
        {
            entity.ToTable("PayoutScheduleItem");
            entity.HasIndex(i => i.EscrowLedgerEntryId).IsUnique();
            entity.Property(i => i.Amount).HasColumnType("decimal(18,6)");
            entity
                .HasOne(i => i.PayoutSchedule)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.PayoutScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(i => i.EscrowEntry)
                .WithOne()
                .HasForeignKey<PayoutScheduleItem>(i => i.EscrowLedgerEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CommissionInvoice>(entity =>
        {
            entity.ToTable("CommissionInvoice");
            entity.HasIndex(i => new { i.SellerId, i.PeriodStart, i.PeriodEnd }).IsUnique();
            entity.Property(i => i.SellerId).HasMaxLength(200);
            entity.Property(i => i.SellerName).HasMaxLength(200);
            entity.Property(i => i.Status).HasMaxLength(50);
            entity.Property(i => i.Number).HasMaxLength(100);
            entity.Property(i => i.Currency).HasMaxLength(10);
            entity.Property(i => i.TaxRate).HasColumnType("decimal(9,6)");
            entity.Property(i => i.Subtotal).HasColumnType("decimal(18,6)");
            entity.Property(i => i.TaxAmount).HasColumnType("decimal(18,6)");
            entity.Property(i => i.TotalAmount).HasColumnType("decimal(18,6)");
        });

        modelBuilder.Entity<CommissionInvoiceLine>(entity =>
        {
            entity.ToTable("CommissionInvoiceLine");
            entity.HasIndex(l => l.EscrowLedgerEntryId);
            entity.Property(l => l.Description).HasMaxLength(500);
            entity.Property(l => l.Amount).HasColumnType("decimal(18,6)");
            entity
                .HasOne(l => l.CommissionInvoice)
                .WithMany(i => i.Lines)
                .HasForeignKey(l => l.CommissionInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(l => l.EscrowLedgerEntry)
                .WithMany()
                .HasForeignKey(l => l.EscrowLedgerEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
