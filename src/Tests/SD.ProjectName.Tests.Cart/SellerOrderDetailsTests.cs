using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Infrastructure;
using Xunit;

namespace SD.ProjectName.Tests.Cart;

public class SellerOrderDetailsTests
{
    [Fact]
    public async Task GetSellerOrderAsync_ReturnsOrderWithDetailsForOwner()
    {
        var repo = CreateRepository(nameof(GetSellerOrderAsync_ReturnsOrderWithDetailsForOwner));
        var order = await SeedOrderAsync(repo);

        var result = await repo.GetSellerOrderAsync(order.SubOrders[0].Id, "seller-1");

        Assert.NotNull(result);
        Assert.Equal(order.Id, result!.OrderId);
        Assert.Equal("buyer-1", result.Order!.BuyerId);
        Assert.Single(result.Items);
        Assert.Equal("Widget", result.Items[0].ProductName);
        Assert.NotNull(result.ShippingSelection);
        Assert.Equal("Express", result.ShippingSelection!.ShippingMethod);
    }

    [Fact]
    public async Task GetSellerOrderAsync_ReturnsNullForDifferentSeller_ButLookupByIdSucceeds()
    {
        var repo = CreateRepository(nameof(GetSellerOrderAsync_ReturnsNullForDifferentSeller_ButLookupByIdSucceeds));
        var order = await SeedOrderAsync(repo);
        var subOrderId = order.SubOrders[0].Id;

        var unauthorizedResult = await repo.GetSellerOrderAsync(subOrderId, "seller-2");
        var existing = await repo.GetSellerOrderByIdAsync(subOrderId);

        Assert.Null(unauthorizedResult);
        Assert.NotNull(existing);
        Assert.Equal("seller-1", existing!.SellerId);
    }

    private static CartRepository CreateRepository(string dbName)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new CartRepository(new CartDbContext(options));
    }

    private static async Task<OrderModel> SeedOrderAsync(CartRepository repo)
    {
        var sellerOrder = new SellerOrderModel
        {
            SellerId = "seller-1",
            SellerName = "Store",
            ItemsSubtotal = 20m,
            ShippingTotal = 5m,
            TotalAmount = 25m,
            Status = OrderStatus.Paid
        };

        var item = new OrderItemModel
        {
            ProductId = 1,
            ProductSku = "SKU-1",
            ProductName = "Widget",
            SellerId = sellerOrder.SellerId,
            SellerName = sellerOrder.SellerName,
            UnitPrice = 10m,
            Quantity = 2,
            SellerOrder = sellerOrder
        };

        var shipping = new OrderShippingSelectionModel
        {
            SellerId = sellerOrder.SellerId,
            SellerName = sellerOrder.SellerName,
            ShippingMethod = "Express",
            Cost = 5m,
            EstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(2),
            SellerOrder = sellerOrder
        };

        var order = new OrderModel
        {
            BuyerId = "buyer-1",
            PaymentMethod = "Card",
            DeliveryRecipientName = "Buyer Name",
            DeliveryLine1 = "123 Street",
            DeliveryCity = "Town",
            DeliveryRegion = "Region",
            DeliveryPostalCode = "12345",
            DeliveryCountryCode = "US",
            ItemsSubtotal = 20m,
            ShippingTotal = 5m,
            TotalAmount = 25m,
            CreatedAt = DateTimeOffset.UtcNow,
            SubOrders = new List<SellerOrderModel> { sellerOrder },
            Items = new List<OrderItemModel> { item },
            ShippingSelections = new List<OrderShippingSelectionModel> { shipping }
        };

        sellerOrder.ShippingSelection = shipping;
        sellerOrder.Items.Add(item);

        await repo.AddOrderAsync(order);
        return order;
    }
}
