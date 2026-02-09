using Microsoft.Extensions.Logging;
using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.Tests.Cart;

public class PaymentProcessingServiceTests
{
    [Fact]
    public async Task HandleCallbackAsync_ReturnsAlreadyProcessed_OnRepeatedSuccess()
    {
        var buyerId = "buyer-pp";
        var selection = new PaymentSelectionModel
        {
            BuyerId = buyerId,
            PaymentMethod = "Card",
            Status = PaymentStatus.Pending,
            ProviderReference = "ref-1",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var cartItems = new List<CartItemModel>
        {
            new() { ProductId = 1, ProductName = "Test", ProductSku = "SKU", Quantity = 1, UnitPrice = 20m, SellerId = "seller-1", SellerName = "Seller" }
        };

        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetPaymentSelectionByReferenceAsync("ref-1")).ReturnsAsync(selection);
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(cartItems);
        repo.Setup(r => r.GetPaymentSelectionAsync(buyerId)).ReturnsAsync(selection);
        repo.Setup(r => r.GetSelectedAddressAsync(buyerId))
            .ReturnsAsync(new DeliveryAddressModel { BuyerId = buyerId, RecipientName = "Buyer" });
        repo.Setup(r => r.GetShippingSelectionsAsync(buyerId))
            .ReturnsAsync(new List<ShippingSelectionModel> { new() { BuyerId = buyerId, SellerId = "seller-1", ShippingMethod = "Standard", Cost = 5m } });
        repo.Setup(r => r.GetShippingRulesAsync()).ReturnsAsync(new List<ShippingRuleModel>
        {
            new() { SellerId = "seller-1", ShippingMethod = "Standard", BasePrice = 5m, IsActive = true }
        });
        repo.Setup(r => r.AddOrderAsync(It.IsAny<OrderModel>()))
            .ReturnsAsync((OrderModel order) =>
            {
                order.Id = 101;
                return order;
            });
        repo.Setup(r => r.GetPromoSelectionAsync(It.IsAny<string>())).ReturnsAsync((PromoSelectionModel?)null);
        repo.Setup(r => r.ClearCartItemsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearShippingSelectionsAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.ClearPromoSelectionAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var snapshotService = new Mock<IProductSnapshotService>();
        snapshotService.Setup(s => s.GetSnapshotAsync(1)).ReturnsAsync(new ProductSnapshot(1, 20m, 2));

        var validationService = new CheckoutValidationService(new GetCartItems(repo.Object), repo.Object, snapshotService.Object);
        var promoService = new PromoService(repo.Object, new GetCartItems(repo.Object), new CartCalculationService(), TimeProvider.System);
        var placeOrder = new PlaceOrder(validationService, repo.Object, new CartCalculationService(), promoService, TimeProvider.System);
        var service = new PaymentProcessingService(repo.Object, placeOrder, TimeProvider.System, Mock.Of<ILogger<PaymentProcessingService>>());

        var first = await service.HandleCallbackAsync("ref-1", "success");

        Assert.True(first.Success);
        Assert.False(first.AlreadyProcessed);
        Assert.Equal(101, selection.OrderId);
        Assert.Equal(PaymentStatus.Authorized, selection.Status);

        var second = await service.HandleCallbackAsync("ref-1", "success");

        Assert.True(second.Success);
        Assert.True(second.AlreadyProcessed);
        Assert.Equal(101, second.OrderId);
    }
}
