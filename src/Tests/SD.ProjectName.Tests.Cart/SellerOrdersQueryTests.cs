using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Infrastructure;
using Xunit;

namespace SD.ProjectName.Tests.Cart;

public class SellerOrdersQueryTests
{
    [Fact]
    public async Task GetSellerOrdersAsync_FiltersByStatusDateAndBuyer()
    {
        var repo = CreateRepository(nameof(GetSellerOrdersAsync_FiltersByStatusDateAndBuyer));
        var seeded = await SeedOrdersAsync(repo);
        var anchor = seeded.Max(s => s.Order!.CreatedAt);

        var result = await repo.GetSellerOrdersAsync(
            "seller-1",
            new SellerOrdersQuery
            {
                Statuses = new[] { OrderStatus.Paid, OrderStatus.Preparing },
                CreatedFrom = anchor.AddDays(-2),
                CreatedTo = anchor.AddMinutes(1),
                BuyerId = "buyer-1",
                Page = 1,
                PageSize = 10
            });

        Assert.Equal(1, result.TotalCount);
        var order = Assert.Single(result.Orders);
        Assert.Equal(OrderStatus.Preparing, order.Status);
        Assert.Equal("buyer-1", order.Order!.BuyerId);
    }

    [Fact]
    public async Task GetSellerOrdersAsync_PaginatesNewestFirst()
    {
        var repo = CreateRepository(nameof(GetSellerOrdersAsync_PaginatesNewestFirst));
        var seeded = await SeedOrdersAsync(repo);
        var sellerOrders = seeded.Where(s => s.SellerId == "seller-1").ToList();
        var oldest = sellerOrders.Min(s => s.Order!.CreatedAt);

        var result = await repo.GetSellerOrdersAsync(
            "seller-1",
            new SellerOrdersQuery
            {
                Page = 2,
                PageSize = 2
            });

        Assert.Equal(sellerOrders.Count, result.TotalCount);
        Assert.Equal(2, result.Page);
        var returned = Assert.Single(result.Orders);
        Assert.Equal(oldest, returned.Order!.CreatedAt);
    }

    private static CartRepository CreateRepository(string dbName)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new CartRepository(new CartDbContext(options));
    }

    private static async Task<List<SellerOrderModel>> SeedOrdersAsync(CartRepository repo)
    {
        var now = DateTimeOffset.UtcNow;
        var orders = new List<OrderModel>
        {
            CreateOrder("buyer-1", "seller-1", OrderStatus.Paid, now.AddDays(-4)),
            CreateOrder("buyer-1", "seller-1", OrderStatus.Preparing, now.AddDays(-1)),
            CreateOrder("buyer-2", "seller-1", OrderStatus.Delivered, now.AddDays(-3)),
            CreateOrder("buyer-1", "seller-2", OrderStatus.Paid, now)
        };

        foreach (var order in orders)
        {
            await repo.AddOrderAsync(order);
        }

        return orders.SelectMany(o => o.SubOrders).ToList();
    }

    private static OrderModel CreateOrder(string buyerId, string sellerId, string status, DateTimeOffset createdAt)
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
