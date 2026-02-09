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

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, ReturnRequestType.Return, "Not as expected", "Package damaged");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Request);
        Assert.Equal(ReturnRequestStatus.Requested, result.Request!.Status);
        Assert.Equal(ReturnRequestType.Return, result.Request.RequestType);
        Assert.Equal("Package damaged", result.Request.Description);
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

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, ReturnRequestType.Return, "Too late", "Requested after window");

        Assert.False(result.IsSuccess);
        Assert.Equal("Request window has expired.", result.Error);
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

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, ReturnRequestType.Return, "Not delivered", "Still in transit");

        Assert.False(result.IsSuccess);
        Assert.Equal("Return or complaint requests are available after delivery.", result.Error);
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

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 999 }, ReturnRequestType.Return, "Mismatch", "Wrong item id");

        Assert.False(result.IsSuccess);
        Assert.Equal("Selected items are not valid for this sub-order.", result.Error);
        repo.Verify(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AllowsComplaint_WhenDelivered()
    {
        var now = new DateTimeOffset(2025, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var order = BuildOrder(now.AddDays(-3), OrderStatus.Delivered, deliveredAt: now.AddDays(-2));
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetOrderAsync(1, "buyer-1")).ReturnsAsync(order);
        repo.Setup(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()))
            .ReturnsAsync((ReturnRequestModel request) => request);
        var service = new ReturnRequestService(repo.Object, new FixedTimeProvider(now));

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, ReturnRequestType.Complaint, "Damaged item", "Screen cracked");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Request);
        Assert.Equal(ReturnRequestType.Complaint, result.Request!.RequestType);
        repo.Verify(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenRequestTypeMissing()
    {
        var now = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var order = BuildOrder(now.AddDays(-1), OrderStatus.Delivered, deliveredAt: now.AddDays(-1));
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetOrderAsync(1, "buyer-1")).ReturnsAsync(order);
        var service = new ReturnRequestService(repo.Object, new FixedTimeProvider(now));

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, string.Empty, "Reason", "Description");

        Assert.False(result.IsSuccess);
        Assert.Equal("Please choose request type.", result.Error);
        repo.Verify(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenDescriptionMissing()
    {
        var now = new DateTimeOffset(2025, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var order = BuildOrder(now.AddDays(-1), OrderStatus.Delivered, deliveredAt: now.AddDays(-1));
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetOrderAsync(1, "buyer-1")).ReturnsAsync(order);
        var service = new ReturnRequestService(repo.Object, new FixedTimeProvider(now));

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, ReturnRequestType.Return, "Reason", string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal("Please provide a description.", result.Error);
        repo.Verify(r => r.AddReturnRequestAsync(It.IsAny<ReturnRequestModel>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenOpenCaseExistsForSelectedItem()
    {
        var now = new DateTimeOffset(2025, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var order = BuildOrder(now.AddDays(-1), OrderStatus.Delivered, deliveredAt: now.AddDays(-1));
        order.SubOrders[0].ReturnRequests.Add(new ReturnRequestModel
        {
            Status = ReturnRequestStatus.Requested,
            Items = new List<ReturnRequestItemModel>
            {
                new()
                {
                    OrderItemId = 100,
                    Quantity = 1
                }
            }
        });
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetOrderAsync(1, "buyer-1")).ReturnsAsync(order);
        var service = new ReturnRequestService(repo.Object, new FixedTimeProvider(now));

        var result = await service.CreateAsync(1, 10, "buyer-1", new List<int> { 100 }, ReturnRequestType.Return, "Reason", "More details");

        Assert.False(result.IsSuccess);
        Assert.Equal("An open case already exists for these items.", result.Error);
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
