using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart;

public class PlaceOrderTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsPriceChangeIssue_WhenPriceDiffers()
    {
        var buyerId = "buyer-1";
        var cartItems = new List<CartItemModel>
        {
            new() { ProductId = 1, ProductName = "Test", UnitPrice = 10m, Quantity = 1, SellerId = "seller-1" }
        };

        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(cartItems);
        repo.Setup(r => r.GetPaymentSelectionAsync(buyerId))
            .ReturnsAsync(new PaymentSelectionModel { BuyerId = buyerId, PaymentMethod = "Card", Status = PaymentStatus.Authorized });
        repo.Setup(r => r.GetSelectedAddressAsync(buyerId))
            .ReturnsAsync(new DeliveryAddressModel { BuyerId = buyerId, RecipientName = "John Doe" });
        repo.Setup(r => r.GetShippingSelectionsAsync(buyerId))
            .ReturnsAsync(new List<ShippingSelectionModel> { new() { BuyerId = buyerId, SellerId = "seller-1", ShippingMethod = "Standard" } });

        var snapshotService = new Mock<IProductSnapshotService>();
        snapshotService.Setup(s => s.GetSnapshotAsync(1)).ReturnsAsync(new ProductSnapshot(1, 12m, 5));

        var service = new CheckoutValidationService(new GetCartItems(repo.Object), repo.Object, snapshotService.Object);

        var result = await service.ValidateAsync(buyerId);

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("price-changed", issue.Code);
        Assert.Equal(12m, issue.CurrentPrice);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesOrderSnapshot_WhenValidationPasses()
    {
        var buyerId = "buyer-2";
        var cartItems = new List<CartItemModel>
        {
            new()
            {
                ProductId = 2,
                ProductName = "Item",
                ProductSku = "SKU-1",
                UnitPrice = 15m,
                Quantity = 2,
                SellerId = "seller-2",
                SellerName = "Seller"
            }
        };

        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(cartItems);
        repo.Setup(r => r.GetPaymentSelectionAsync(buyerId))
            .ReturnsAsync(new PaymentSelectionModel { BuyerId = buyerId, PaymentMethod = "Card", Status = PaymentStatus.Authorized });
        repo.Setup(r => r.GetSelectedAddressAsync(buyerId))
            .ReturnsAsync(new DeliveryAddressModel { BuyerId = buyerId, RecipientName = "Jane Doe" });
        repo.Setup(r => r.GetShippingSelectionsAsync(buyerId))
            .ReturnsAsync(new List<ShippingSelectionModel> { new() { BuyerId = buyerId, SellerId = "seller-2", ShippingMethod = "Standard", Cost = 5m } });
        repo.Setup(r => r.GetShippingRulesAsync()).ReturnsAsync(new List<ShippingRuleModel>
        {
            new() { SellerId = "seller-2", ShippingMethod = "Standard", BasePrice = 5m, IsActive = true }
        });
        repo.Setup(r => r.AddOrderAsync(It.IsAny<OrderModel>())).ReturnsAsync((OrderModel order) => order);
        repo.Setup(r => r.ClearCartItemsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearShippingSelectionsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearPaymentSelectionAsync(buyerId)).Returns(Task.CompletedTask);

        var snapshotService = new Mock<IProductSnapshotService>();
        snapshotService.Setup(s => s.GetSnapshotAsync(2)).ReturnsAsync(new ProductSnapshot(2, 15m, 10));

        var validationService = new CheckoutValidationService(new GetCartItems(repo.Object), repo.Object, snapshotService.Object);
        var handler = new PlaceOrder(validationService, repo.Object, new CartCalculationService(), TimeProvider.System);

        var result = await handler.ExecuteAsync(buyerId);

        Assert.True(result.Success);
        Assert.NotNull(result.Order);
        Assert.Equal(30m, result.Order!.ItemsSubtotal);
        Assert.Equal(5m, result.Order.ShippingTotal);
        Assert.Equal(35m, result.Order.TotalAmount);
        Assert.Equal(OrderStatus.Confirmed, result.Order.Status);
        var shippingSelection = Assert.Single(result.Order.ShippingSelections);
        Assert.Equal("seller-2", shippingSelection.SellerId);
        Assert.Equal("Standard", shippingSelection.ShippingMethod);
        var item = Assert.Single(result.Order.Items);
        Assert.Equal(15m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
        repo.Verify(r => r.AddOrderAsync(It.IsAny<OrderModel>()), Times.Once);
        repo.Verify(r => r.ClearCartItemsAsync(buyerId), Times.Once);
        repo.Verify(r => r.ClearShippingSelectionsAsync(buyerId), Times.Once);
        repo.Verify(r => r.ClearPaymentSelectionAsync(buyerId), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenStockIsInsufficient()
    {
        var buyerId = "buyer-3";
        var cartItems = new List<CartItemModel>
        {
            new() { ProductId = 3, ProductName = "Low Stock", UnitPrice = 8m, Quantity = 3, SellerId = "seller-3" }
        };

        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(cartItems);
        repo.Setup(r => r.GetPaymentSelectionAsync(buyerId))
            .ReturnsAsync(new PaymentSelectionModel { BuyerId = buyerId, PaymentMethod = "Card", Status = PaymentStatus.Authorized });
        repo.Setup(r => r.GetSelectedAddressAsync(buyerId))
            .ReturnsAsync(new DeliveryAddressModel { BuyerId = buyerId, RecipientName = "Sam Buyer" });
        repo.Setup(r => r.GetShippingSelectionsAsync(buyerId))
            .ReturnsAsync(new List<ShippingSelectionModel> { new() { BuyerId = buyerId, SellerId = "seller-3", ShippingMethod = "Standard" } });

        var snapshotService = new Mock<IProductSnapshotService>();
        snapshotService.Setup(s => s.GetSnapshotAsync(3)).ReturnsAsync(new ProductSnapshot(3, 8m, 1));

        var validationService = new CheckoutValidationService(new GetCartItems(repo.Object), repo.Object, snapshotService.Object);
        var handler = new PlaceOrder(validationService, repo.Object, new CartCalculationService(), TimeProvider.System);

        var result = await handler.ExecuteAsync(buyerId);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Code == "insufficient-stock");
        repo.Verify(r => r.AddOrderAsync(It.IsAny<OrderModel>()), Times.Never);
    }
}
