using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart;

public class ReturnRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_AllowsRequest_WhenDeliveredWithinWindow()
    {
        var now = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var order = BuildOrder(now.AddDays(-2), OrderStatus.Delivered, deliveredAt: now.AddDays(-2));
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetOrderAsync(1, "buyer-1")).ReturnsAsync(order);
        repo.Setup(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()))
            .ReturnsAsync((ReturnRequestModel request) => request);
        var service = new ReturnRequestService(repo.Object, new FixedTimeProvider(now));

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, "Not as expected");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Request);
        Assert.Equal(ReturnRequestStatus.Requested, result.Request!.Status);
        Assert.Single(result.Request.Items);
        Assert.Equal(100, result.Request.Items[0].OrderItemId);
        repo.Verify(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenReturnWindowExpired()
    {
        var now = new DateTimeOffset(2025, 2, 1, 12, 0, 0, TimeSpan.Zero);
        var order = BuildOrder(now.AddDays(-30), OrderStatus.Delivered, deliveredAt: now.AddDays(-30));
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetOrderAsync(1, "buyer-1")).ReturnsAsync(order);
        var service = new ReturnRequestService(repo.Object, new FixedTimeProvider(now));

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, "Too late");

        Assert.False(result.IsSuccess);
        Assert.Equal("Return window has expired.", result.Error);
        repo.Verify(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenSubOrderNotDelivered()
    {
        var now = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var order = BuildOrder(now.AddDays(-1), OrderStatus.Shipped, deliveredAt: null);
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetOrderAsync(1, "buyer-1")).ReturnsAsync(order);
        var service = new ReturnRequestService(repo.Object, new FixedTimeProvider(now));

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, "Not delivered");

        Assert.False(result.IsSuccess);
        Assert.Equal("Return requests are available after delivery.", result.Error);
        repo.Verify(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenItemsDoNotMatchSubOrder()
    {
        var now = new DateTimeOffset(2025, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var order = BuildOrder(now.AddDays(-1), OrderStatus.Delivered, deliveredAt: now.AddDays(-1));
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetOrderAsync(1, "buyer-1")).ReturnsAsync(order);
        var service = new ReturnRequestService(repo.Object, new FixedTimeProvider(now));

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 999 }, "Mismatch");

        Assert.False(result.IsSuccess);
        Assert.Equal("Selected items are not valid for this sub-order.", result.Error);
        repo.Verify(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()), Times.Never);
    }

    private static OrderModel BuildOrder(DateTimeOffset createdAt, string status, DateTimeOffset? deliveredAt)
    {
        var item = new OrderItemModel
        {
            Id = 100,
            ProductId = 1,
            ProductName = "Item",
            SellerId = "seller-1",
            UnitPrice = 10m,
            Quantity = 1
        };

        var subOrder = new SellerOrderModel
        {
            Id = 10,
            OrderId = 1,
            SellerId = "seller-1",
            Status = status,
            DeliveredAt = deliveredAt,
            Items = new List<OrderItemModel> { item }
        };

        return new OrderModel
        {
            Id = 1,
            BuyerId = "buyer-1",
            CreatedAt = createdAt,
            SubOrders = new List<SellerOrderModel> { subOrder }
        };
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
