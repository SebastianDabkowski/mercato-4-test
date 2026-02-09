using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Infrastructure;

namespace SD.ProjectName.Tests.Cart;

public class OrderStatusServiceTests
{
    [Fact]
    public async Task UpdateSellerOrderStatusAsync_MovesPaidToPreparing()
    {
        var repo = CreateRepository(nameof(UpdateSellerOrderStatusAsync_MovesPaidToPreparing));
        var order = await SeedOrderAsync(repo, OrderStatus.Paid);
        var service = CreateService(repo);

        var result = await service.UpdateSellerOrderStatusAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId, OrderStatus.Preparing);

        Assert.True(result.IsSuccess);
        var updated = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId);
        Assert.Equal(OrderStatus.Preparing, updated!.Status);
        Assert.Equal(OrderStatus.Preparing, updated.Order!.Status);
    }

    [Fact]
    public async Task UpdateSellerOrderStatusAsync_BlocksInvalidJump()
    {
        var repo = CreateRepository(nameof(UpdateSellerOrderStatusAsync_BlocksInvalidJump));
        var order = await SeedOrderAsync(repo, OrderStatus.Paid);
        var service = CreateService(repo);

        var result = await service.UpdateSellerOrderStatusAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId, OrderStatus.Delivered);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderStatus.Paid, order.SubOrders[0].Status);
    }

    [Fact]
    public async Task UpdateSellerOrderStatusAsync_SetsTrackingWhenShipped()
    {
        var repo = CreateRepository(nameof(UpdateSellerOrderStatusAsync_SetsTrackingWhenShipped));
        var order = await SeedOrderAsync(repo, OrderStatus.Preparing);
        var service = CreateService(repo);

        var result = await service.UpdateSellerOrderStatusAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId, OrderStatus.Shipped, trackingNumber: "TRACK-123");

        Assert.True(result.IsSuccess);
        var updated = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId);
        Assert.Equal("TRACK-123", updated!.TrackingNumber);
        Assert.Equal(OrderStatus.Shipped, updated.Order!.Status);
    }

    [Fact]
    public async Task CancelOrderAsync_BlocksWhenShipmentStarted()
    {
        var repo = CreateRepository(nameof(CancelOrderAsync_BlocksWhenShipmentStarted));
        var order = await SeedOrderAsync(repo, OrderStatus.Shipped);
        var service = CreateService(repo);

        var result = await service.CancelOrderAsync(order.Id, order.BuyerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, order.SubOrders[0].Status);
    }

    [Fact]
    public async Task CancelOrderAsync_ReleasesEscrow()
    {
        var repo = CreateRepository(nameof(CancelOrderAsync_ReleasesEscrow));
        var order = await SeedOrderAsync(repo, OrderStatus.Paid);
        var escrow = new EscrowLedgerEntry
        {
            OrderId = order.Id,
            SellerOrderId = order.SubOrders[0].Id,
            BuyerId = order.BuyerId,
            SellerId = order.SubOrders[0].SellerId,
            HeldAmount = order.TotalAmount,
            CommissionAmount = 0.15m,
            SellerPayoutAmount = 14.85m,
            Status = EscrowLedgerStatus.Held,
            CreatedAt = DateTimeOffset.UtcNow,
            PayoutEligibleAt = DateTimeOffset.UtcNow
        };
        await repo.AddEscrowEntriesAsync(new List<EscrowLedgerEntry> { escrow });
        var service = CreateService(repo);

        var result = await service.CancelOrderAsync(order.Id, order.BuyerId);

        Assert.True(result.IsSuccess);
        var ledger = await repo.GetEscrowEntriesForOrderAsync(order.Id);
        Assert.Single(ledger);
        Assert.Equal(EscrowLedgerStatus.ReleasedToBuyer, ledger[0].Status);
        Assert.NotNull(ledger[0].ReleasedAt);
        Assert.Equal("Order cancelled", ledger[0].ReleaseReason);
    }

    [Fact]
    public async Task MarkSubOrderDeliveredAsync_UpdatesOrderRollup()
    {
        var repo = CreateRepository(nameof(MarkSubOrderDeliveredAsync_UpdatesOrderRollup));
        var order = await SeedOrderAsync(repo, OrderStatus.Shipped);
        var service = CreateService(repo);

        var result = await service.MarkSubOrderDeliveredAsync(order.Id, order.SubOrders[0].Id, order.BuyerId);

        Assert.True(result.IsSuccess);
        var updatedOrder = await repo.GetOrderAsync(order.Id, order.BuyerId);
        Assert.Equal(OrderStatus.Delivered, updatedOrder!.SubOrders[0].Status);
        Assert.Equal(OrderStatus.Delivered, updatedOrder.Status);
    }

    [Fact]
    public async Task UpdateItemStatusesAsync_AllowsPartialShipment()
    {
        var repo = CreateRepository(nameof(UpdateItemStatusesAsync_AllowsPartialShipment));
        var order = await SeedOrderWithItemsAsync(repo);
        var service = CreateService(repo);

        var firstItemId = order.SubOrders[0].Items[0].Id;
        var secondItemId = order.SubOrders[0].Items[1].Id;

        var result = await service.UpdateItemStatusesAsync(
            order.SubOrders[0].Id,
            order.SubOrders[0].SellerId,
            new[] { firstItemId },
            Array.Empty<int>(),
            "TRACK-PARTIAL");

        Assert.True(result.IsSuccess);
        var updated = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId);
        Assert.Equal(OrderStatus.Shipped, updated!.Items.First(i => i.Id == firstItemId).Status);
        Assert.Equal(OrderStatus.Preparing, updated.Items.First(i => i.Id == secondItemId).Status);
        Assert.Equal(OrderStatus.Preparing, updated.Status);
        Assert.Equal("TRACK-PARTIAL", updated.TrackingNumber);
    }

    [Fact]
    public async Task UpdateItemStatusesAsync_CancelsItemsAndCalculatesRefund()
    {
        var repo = CreateRepository(nameof(UpdateItemStatusesAsync_CancelsItemsAndCalculatesRefund));
        var order = await SeedOrderWithItemsAsync(repo);
        var service = CreateService(repo);

        var cancelItemId = order.SubOrders[0].Items[0].Id;

        var result = await service.UpdateItemStatusesAsync(
            order.SubOrders[0].Id,
            order.SubOrders[0].SellerId,
            Array.Empty<int>(),
            new[] { cancelItemId },
            null);

        Assert.True(result.IsSuccess);
        var updated = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId);
        Assert.Equal(OrderStatus.Cancelled, updated!.Items.First(i => i.Id == cancelItemId).Status);
        Assert.Equal(16m, updated.RefundedAmount);
        Assert.Equal(16m, updated.Order!.RefundedAmount);
    }

    [Fact]
    public async Task MarkSubOrderDeliveredAsync_SetsItemsDelivered()
    {
        var repo = CreateRepository(nameof(MarkSubOrderDeliveredAsync_SetsItemsDelivered));
        var order = await SeedOrderWithItemsAsync(repo, OrderStatus.Shipped, OrderStatus.Shipped);
        var service = CreateService(repo);

        var result = await service.MarkSubOrderDeliveredAsync(order.Id, order.SubOrders[0].Id, order.BuyerId);

        Assert.True(result.IsSuccess);
        var updated = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId);
        Assert.All(updated!.Items, i => Assert.Equal(OrderStatus.Delivered, i.Status));
        Assert.Equal(OrderStatus.Delivered, updated.Status);
    }

    private static OrderStatusService CreateService(CartRepository repo)
    {
        var commissionService = new CommissionService(Options.Create(new CommissionOptions()), TimeProvider.System);
        var escrowService = new EscrowService(repo, TimeProvider.System, commissionService, Options.Create(new EscrowOptions()));
        return new OrderStatusService(repo, escrowService, commissionService);
    }

    private static CartRepository CreateRepository(string dbName)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new CartRepository(new CartDbContext(options));
    }

    private static async Task<OrderModel> SeedOrderAsync(CartRepository repo, string subOrderStatus)
    {
        var order = new OrderModel
        {
            BuyerId = "buyer-1",
            PaymentMethod = "Card",
            DeliveryRecipientName = "Buyer",
            DeliveryLine1 = "123 Street",
            DeliveryCity = "Town",
            DeliveryRegion = "Region",
            DeliveryPostalCode = "12345",
            DeliveryCountryCode = "US",
            ItemsSubtotal = 10m,
            ShippingTotal = 5m,
            TotalAmount = 15m,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = OrderStatus.Paid,
            SubOrders = new List<SellerOrderModel>
            {
                new()
                {
                    SellerId = "seller-1",
                    SellerName = "Seller",
                    ItemsSubtotal = 10m,
                    ShippingTotal = 5m,
                    TotalAmount = 15m,
                    Status = subOrderStatus
                }
            }
        };

        await repo.AddOrderAsync(order);
        return order;
    }

    private static async Task<OrderModel> SeedOrderWithItemsAsync(
        CartRepository repo,
        string subOrderStatus = OrderStatus.Preparing,
        string itemStatus = OrderStatus.Preparing)
    {
        var item1 = new OrderItemModel
        {
            ProductId = 1,
            ProductSku = "SKU-1",
            ProductName = "First",
            SellerId = "seller-1",
            SellerName = "Seller",
            UnitPrice = 20m,
            Quantity = 1,
            Status = itemStatus
        };

        var item2 = new OrderItemModel
        {
            ProductId = 2,
            ProductSku = "SKU-2",
            ProductName = "Second",
            SellerId = "seller-1",
            SellerName = "Seller",
            UnitPrice = 10m,
            Quantity = 1,
            Status = itemStatus
        };

        var subOrder = new SellerOrderModel
        {
            SellerId = "seller-1",
            SellerName = "Seller",
            ItemsSubtotal = 30m,
            ShippingTotal = 5m,
            DiscountTotal = 6m,
            TotalAmount = 29m,
            Status = subOrderStatus,
            Items = new List<OrderItemModel> { item1, item2 }
        };

        item1.SellerOrder = subOrder;
        item2.SellerOrder = subOrder;

        var order = new OrderModel
        {
            BuyerId = "buyer-1",
            PaymentMethod = "Card",
            DeliveryRecipientName = "Buyer",
            DeliveryLine1 = "123 Street",
            DeliveryCity = "Town",
            DeliveryRegion = "Region",
            DeliveryPostalCode = "12345",
            DeliveryCountryCode = "US",
            ItemsSubtotal = 30m,
            ShippingTotal = 5m,
            DiscountTotal = 6m,
            TotalAmount = 29m,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = OrderStatus.Paid,
            Items = new List<OrderItemModel> { item1, item2 },
            SubOrders = new List<SellerOrderModel> { subOrder }
        };

        await repo.AddOrderAsync(order);
        return order;
    }
}
