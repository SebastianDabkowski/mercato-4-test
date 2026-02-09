using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Infrastructure;
using Xunit;

namespace SD.ProjectName.Tests.Cart;

public class ShippingIntegrationServiceTests
{
    [Fact]
    public async Task EnableProviderForSellerAsync_AddsRulesForProviderServices()
    {
        var repo = CreateRepository(nameof(EnableProviderForSellerAsync_AddsRulesForProviderServices));
        var options = BuildOptions();
        var service = CreateService(repo, new FakeShippingProviderClient(), options);

        var result = await service.EnableProviderForSellerAsync("seller-123", "InPost");

        Assert.True(result.IsSuccess);
        var rules = await repo.GetShippingRulesAsync();
        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, r => r.ShippingMethod == "InPost Locker" && r.BasePrice == 12.99m);
        Assert.Contains(rules, r => r.ShippingMethod == "InPost Courier" && r.BasePrice == 19.99m);
    }

    [Fact]
    public async Task CreateShipmentAsync_CreatesTrackingAndMarksShipped()
    {
        var repo = CreateRepository(nameof(CreateShipmentAsync_CreatesTrackingAndMarksShipped));
        var options = BuildOptions();
        var order = await SeedOrderAsync(repo, "InPost Locker");
        var client = new FakeShippingProviderClient
        {
            Result = new ShippingProviderShipmentResult(
                true,
                "TRACK-XYZ",
                "https://track.test/TRACK-XYZ",
                "InPost",
                LabelContent: new byte[] { 1, 2, 3 },
                LabelContentType: "application/pdf",
                LabelFileName: "label.pdf")
        };
        var service = CreateService(repo, client, options);

        var itemIds = order.SubOrders[0].Items.Select(i => i.Id).ToArray();
        var result = await service.CreateShipmentAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId, itemIds);

        Assert.True(result.IsSuccess);
        Assert.Equal("TRACK-XYZ", result.TrackingNumber);
        var updated = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId);
        Assert.Equal(OrderStatus.Shipped, updated!.Status);
        Assert.Equal("TRACK-XYZ", updated.TrackingNumber);
        Assert.Equal("InPost", updated.TrackingCarrier);
        Assert.Equal(OrderStatus.Shipped, updated.Order!.Status);
        Assert.NotNull(updated.ShippingLabel);
        Assert.Equal("label.pdf", updated.ShippingLabelFileName);
    }

    [Fact]
    public async Task CreateShipmentAsync_FailsWhenLabelMissingForLabelEnabledService()
    {
        var repo = CreateRepository(nameof(CreateShipmentAsync_FailsWhenLabelMissingForLabelEnabledService));
        var options = BuildOptions();
        var order = await SeedOrderAsync(repo, "InPost Locker");
        var client = new FakeShippingProviderClient
        {
            Result = new ShippingProviderShipmentResult(true, "TRACK-MISS", "https://track.test/TRACK-MISS", "InPost")
        };
        var service = CreateService(repo, client, options);

        var itemIds = order.SubOrders[0].Items.Select(i => i.Id).ToArray();
        var result = await service.CreateShipmentAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId, itemIds);

        Assert.False(result.IsSuccess);
        Assert.Contains("label", result.Error, StringComparison.OrdinalIgnoreCase);
        var updated = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId);
        Assert.Null(updated!.TrackingNumber);
        Assert.Equal(OrderStatus.Preparing, updated.Status);
    }

    [Fact]
    public async Task ApplyStatusUpdateAsync_MovesOrderToDelivered()
    {
        var repo = CreateRepository(nameof(ApplyStatusUpdateAsync_MovesOrderToDelivered));
        var options = BuildOptions();
        var order = await SeedOrderAsync(repo, "InPost Locker", OrderStatus.Shipped);
        order.SubOrders[0].TrackingNumber = "TRACK-DELIVER";
        await repo.SaveChangesAsync();
        var service = CreateService(repo, new FakeShippingProviderClient(), options);

        var result = await service.ApplyStatusUpdateAsync(new ShippingStatusUpdate("InPost", "TRACK-DELIVER", "delivered"));

        Assert.True(result.IsSuccess);
        var updated = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId);
        Assert.Equal(OrderStatus.Delivered, updated!.Status);
        Assert.Equal(OrderStatus.Delivered, updated.Order!.Status);
    }

    private static ShippingIntegrationService CreateService(
        CartRepository repo,
        IShippingProviderClient client,
        ShippingProvidersOptions options)
    {
        return new ShippingIntegrationService(
            repo,
            CreateOrderStatusService(repo),
            client,
            Options.Create(options));
    }

    private static OrderStatusService CreateOrderStatusService(CartRepository repo)
    {
        var commissionService = new CommissionService(Options.Create(new CommissionOptions()), TimeProvider.System);
        var escrowService = new EscrowService(repo, TimeProvider.System, commissionService, Options.Create(new EscrowOptions()));
        return new OrderStatusService(repo, escrowService, commissionService, TimeProvider.System);
    }

    private static CartRepository CreateRepository(string dbName)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new CartRepository(new CartDbContext(options));
    }

    private static async Task<OrderModel> SeedOrderAsync(
        CartRepository repo,
        string shippingMethod,
        string status = OrderStatus.Preparing)
    {
        var item = new OrderItemModel
        {
            ProductId = 1,
            ProductSku = "SKU-1",
            ProductName = "Test product",
            SellerId = "seller-1",
            SellerName = "Seller",
            UnitPrice = 50m,
            Quantity = 1,
            Status = status
        };

        var subOrder = new SellerOrderModel
        {
            SellerId = "seller-1",
            SellerName = "Seller",
            ItemsSubtotal = 50m,
            ShippingTotal = 10m,
            TotalAmount = 60m,
            Status = status,
            Items = new List<OrderItemModel> { item }
        };

        var selection = new OrderShippingSelectionModel
        {
            SellerId = subOrder.SellerId,
            SellerName = subOrder.SellerName,
            ShippingMethod = shippingMethod,
            Cost = 10m,
            SellerOrder = subOrder
        };
        subOrder.ShippingSelection = selection;
        item.SellerOrder = subOrder;

        var order = new OrderModel
        {
            BuyerId = "buyer-1",
            PaymentMethod = "Card",
            DeliveryRecipientName = "Buyer",
            DeliveryLine1 = "Street 1",
            DeliveryCity = "Town",
            DeliveryRegion = "Region",
            DeliveryPostalCode = "12345",
            DeliveryCountryCode = "PL",
            ItemsSubtotal = 50m,
            ShippingTotal = 10m,
            TotalAmount = 60m,
            Status = OrderStatus.Paid,
            Items = new List<OrderItemModel> { item },
            ShippingSelections = new List<OrderShippingSelectionModel> { selection },
            SubOrders = new List<SellerOrderModel> { subOrder }
        };

        subOrder.Order = order;

        await repo.AddOrderAsync(order);
        return order;
    }

    private static ShippingProvidersOptions BuildOptions()
    {
        return new ShippingProvidersOptions
        {
            Providers = new List<ShippingProviderDefinition>
            {
                new()
                {
                    Code = "InPost",
                    DisplayName = "InPost",
                    Carrier = "InPost",
                    TrackingUrlTemplate = "https://inpost.pl/tracking/{trackingNumber}",
                    Enabled = true,
                    Services = new List<ShippingProviderServiceDefinition>
                    {
                        new()
                        {
                            Code = "locker",
                            Name = "InPost Locker",
                            DefaultPrice = 12.99m,
                            DefaultDeliveryEstimate = "1-3 business days",
                            SupportsLabelCreation = true
                        },
                        new()
                        {
                            Code = "courier",
                            Name = "InPost Courier",
                            DefaultPrice = 19.99m,
                            DefaultDeliveryEstimate = "1-2 business days",
                            SupportsLabelCreation = true
                        }
                    }
                },
                new()
                {
                    Code = "DHL",
                    DisplayName = "DHL",
                    Carrier = "DHL",
                    TrackingUrlTemplate = "https://www.dhl.com/track?tracking-number={trackingNumber}",
                    Enabled = true,
                    Services = new List<ShippingProviderServiceDefinition>
                    {
                        new() { Code = "parcel", Name = "DHL Parcel", DefaultPrice = 22.5m, DefaultDeliveryEstimate = "2-4 business days" }
                    }
                }
            }
        };
    }

    private class FakeShippingProviderClient : IShippingProviderClient
    {
        public ShippingProviderShipmentResult Result { get; set; } = new(
            true,
            "TRACK-123",
            "https://track.test/TRACK-123",
            "Carrier",
            LabelContent: new byte[] { 9, 9, 9 },
            LabelContentType: "application/pdf",
            LabelFileName: "default.pdf");

        public Task<ShippingProviderShipmentResult> CreateShipmentAsync(
            ShippingProviderShipmentRequest request,
            ShippingProviderDefinition provider,
            ShippingProviderServiceDefinition service,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result);
        }
    }
}
