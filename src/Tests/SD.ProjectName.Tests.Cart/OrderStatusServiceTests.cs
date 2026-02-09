using Microsoft.EntityFrameworkCore;
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
        var service = new OrderStatusService(repo);

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
        var service = new OrderStatusService(repo);

        var result = await service.UpdateSellerOrderStatusAsync(order.SubOrders[0].Id, order.SubOrders[0].SellerId, OrderStatus.Delivered);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderStatus.Paid, order.SubOrders[0].Status);
    }

    [Fact]
    public async Task UpdateSellerOrderStatusAsync_SetsTrackingWhenShipped()
    {
        var repo = CreateRepository(nameof(UpdateSellerOrderStatusAsync_SetsTrackingWhenShipped));
        var order = await SeedOrderAsync(repo, OrderStatus.Preparing);
        var service = new OrderStatusService(repo);

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
        var service = new OrderStatusService(repo);

        var result = await service.CancelOrderAsync(order.Id, order.BuyerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, order.SubOrders[0].Status);
    }

    [Fact]
    public async Task MarkSubOrderDeliveredAsync_UpdatesOrderRollup()
    {
        var repo = CreateRepository(nameof(MarkSubOrderDeliveredAsync_UpdatesOrderRollup));
        var order = await SeedOrderAsync(repo, OrderStatus.Shipped);
        var service = new OrderStatusService(repo);

        var result = await service.MarkSubOrderDeliveredAsync(order.Id, order.SubOrders[0].Id, order.BuyerId);

        Assert.True(result.IsSuccess);
        var updatedOrder = await repo.GetOrderAsync(order.Id, order.BuyerId);
        Assert.Equal(OrderStatus.Delivered, updatedOrder!.SubOrders[0].Status);
        Assert.Equal(OrderStatus.Delivered, updatedOrder.Status);
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
}
