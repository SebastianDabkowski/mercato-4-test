using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Infrastructure;
using Xunit;

namespace SD.ProjectName.Tests.Cart;

public class ReturnRequestMessagingTests
{
    [Fact]
    public async Task AddBuyerMessage_IncrementsSellerUnreadAndSavesMessage()
    {
        var (repo, context) = CreateRepository(nameof(AddBuyerMessage_IncrementsSellerUnreadAndSavesMessage));
        var request = await SeedReturnRequestAsync(repo, context, "buyer-1", "seller-1");
        var now = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var message = await repo.AddBuyerReturnRequestMessageAsync(request.Id, "buyer-1", "Need help", now);
        var loaded = await repo.GetReturnRequestAsync(request.Id, "buyer-1");

        Assert.NotNull(message);
        Assert.Equal(1, loaded!.SellerUnreadCount);
        Assert.Equal(now, loaded.UpdatedAt);
        var savedMessage = Assert.Single(loaded.Messages);
        Assert.Equal(ReturnRequestMessageSender.Buyer, savedMessage.SenderRole);
        Assert.Equal("Need help", savedMessage.Body);
    }

    [Fact]
    public async Task AddSellerMessage_ReturnsNullForUnauthorizedSeller()
    {
        var (repo, context) = CreateRepository(nameof(AddSellerMessage_ReturnsNullForUnauthorizedSeller));
        var request = await SeedReturnRequestAsync(repo, context, "buyer-1", "seller-1");

        var result = await repo.AddSellerReturnRequestMessageAsync(request.Id, "seller-2", "Hello", DateTimeOffset.UtcNow);

        Assert.Null(result);
        var loaded = await repo.GetReturnRequestByIdAsync(request.Id);
        Assert.Equal(0, loaded!.BuyerUnreadCount);
    }

    [Fact]
    public async Task MarkBuyerMessagesRead_ClearsUnreadCount()
    {
        var (repo, context) = CreateRepository(nameof(MarkBuyerMessagesRead_ClearsUnreadCount));
        var request = await SeedReturnRequestAsync(repo, context, "buyer-1", "seller-1");
        await repo.AddSellerReturnRequestMessageAsync(request.Id, "seller-1", "Update", DateTimeOffset.UtcNow);

        var cleared = await repo.MarkBuyerMessagesReadAsync(request.Id, "buyer-1");
        var loaded = await repo.GetReturnRequestAsync(request.Id, "buyer-1");

        Assert.True(cleared);
        Assert.Equal(0, loaded!.BuyerUnreadCount);
    }

    private static (CartRepository Repo, CartDbContext Context) CreateRepository(string dbName)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new CartDbContext(options);
        return (new CartRepository(context), context);
    }

    private static async Task<ReturnRequestModel> SeedReturnRequestAsync(
        CartRepository repo,
        CartDbContext context,
        string buyerId,
        string sellerId)
    {
        var order = new OrderModel
        {
            BuyerId = buyerId,
            CreatedAt = DateTimeOffset.UtcNow,
            DeliveryRecipientName = "Test",
            DeliveryLine1 = "Line1",
            DeliveryCity = "City",
            DeliveryRegion = "Region",
            DeliveryPostalCode = "00000",
            DeliveryCountryCode = "US"
        };

        var sellerOrder = new SellerOrderModel
        {
            SellerId = sellerId,
            SellerName = "Store",
            Status = OrderStatus.Delivered,
            Order = order
        };

        order.SubOrders.Add(sellerOrder);

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var request = new ReturnRequestModel
        {
            OrderId = order.Id,
            SellerOrderId = sellerOrder.Id,
            BuyerId = buyerId,
            RequestType = ReturnRequestType.Return,
            Status = ReturnRequestStatus.Requested,
            Reason = "Reason",
            Description = "Details",
            RequestedAt = DateTimeOffset.UtcNow,
            Order = order,
            SellerOrder = sellerOrder
        };

        return await repo.AddReturnRequestAsync(request);
    }
}
