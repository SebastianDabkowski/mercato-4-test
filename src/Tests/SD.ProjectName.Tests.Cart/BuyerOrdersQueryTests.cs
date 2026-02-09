using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Infrastructure;
using Xunit;

namespace SD.ProjectName.Tests.Cart;

public class BuyerOrdersQueryTests
{
    [Fact]
    public async Task GetOrdersForBuyerAsync_FiltersByStatusDateAndSeller()
    {
        var repo = CreateRepository(nameof(GetOrdersForBuyerAsync_FiltersByStatusDateAndSeller));
        var seeded = await SeedOrdersAsync(repo);
        var anchor = seeded.Max(o => o.CreatedAt);

        var result = await repo.GetOrdersForBuyerAsync(
            "buyer-1",
            new BuyerOrdersQuery
            {
                Statuses = new[] { OrderStatus.Paid, OrderStatus.Delivered },
                CreatedFrom = anchor.AddDays(-3),
                CreatedTo = anchor.AddMinutes(1),
                SellerId = "seller-1",
                Page = 1,
                PageSize = 10
            });

        Assert.Equal(1, result.TotalCount);
        var order = Assert.Single(result.Orders);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.All(result.Orders, o => Assert.Equal("buyer-1", o.BuyerId));
    }

    [Fact]
    public async Task GetOrdersForBuyerAsync_PaginatesNewestFirst()
    {
        var repo = CreateRepository(nameof(GetOrdersForBuyerAsync_PaginatesNewestFirst));
        var seeded = await SeedOrdersAsync(repo);
        var oldest = seeded.Where(o => o.BuyerId == "buyer-1").Min(o => o.CreatedAt);

        var result = await repo.GetOrdersForBuyerAsync(
            "buyer-1",
            new BuyerOrdersQuery
            {
                Page = 5,
                PageSize = 2
            });

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Page);
        var returned = Assert.Single(result.Orders);
        Assert.Equal(oldest, returned.CreatedAt);
        Assert.Contains(result.Sellers, s => s.SellerId == "seller-1");
        Assert.Contains(result.Sellers, s => s.SellerId == "seller-2");
    }

    private static CartRepository CreateRepository(string dbName)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new CartRepository(new CartDbContext(options));
    }

    private static async Task<List<OrderModel>> SeedOrdersAsync(CartRepository repo)
    {
        var now = DateTimeOffset.UtcNow;
        var orders = new List<OrderModel>
        {
            CreateOrder("buyer-1", OrderStatus.New, now.AddDays(-5), "seller-1"),
            CreateOrder("buyer-1", OrderStatus.Paid, now.AddDays(-2), "seller-2"),
            CreateOrder("buyer-1", OrderStatus.Delivered, now.AddDays(-1), "seller-1"),
            CreateOrder("buyer-2", OrderStatus.Paid, now, "seller-1")
        };

        foreach (var order in orders)
        {
            await repo.AddOrderAsync(order);
        }

        return orders;
    }

    private static OrderModel CreateOrder(string buyerId, string status, DateTimeOffset createdAt, string sellerId)
    {
        return new OrderModel
        {
            BuyerId = buyerId,
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
            CreatedAt = createdAt,
            Status = status,
            SubOrders = new List<SellerOrderModel>
            {
                new()
                {
                    SellerId = sellerId,
                    SellerName = sellerId,
                    ItemsSubtotal = 10m,
                    ShippingTotal = 5m,
                    TotalAmount = 15m,
                    Status = status
                }
            }
        };
    }
}
