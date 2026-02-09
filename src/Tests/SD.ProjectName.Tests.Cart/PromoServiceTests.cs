using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart;

public class PromoServiceTests
{
    [Fact]
    public async Task ApplyAsync_Succeeds_ForValidPromo()
    {
        var buyerId = "buyer-1";
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetPromoSelectionAsync(buyerId)).ReturnsAsync((PromoSelectionModel?)null);
        repo.Setup(r => r.GetPromoCodeAsync("WELCOME10")).ReturnsAsync(new PromoCodeModel
        {
            Code = "WELCOME10",
            DiscountType = PromoDiscountType.Percentage,
            DiscountValue = 0.1m,
            IsActive = true
        });
        repo.Setup(r => r.GetShippingRulesAsync()).ReturnsAsync(new List<ShippingRuleModel>());
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(new List<CartItemModel>
        {
            new() { SellerId = "seller-1", UnitPrice = 50m, Quantity = 2 }
        });
        repo.Setup(r => r.UpsertPromoSelectionAsync(It.IsAny<PromoSelectionModel>()))
            .ReturnsAsync((PromoSelectionModel selection) => selection);

        var service = new PromoService(repo.Object, new GetCartItems(repo.Object), new CartCalculationService(), TimeProvider.System);

        var result = await service.ApplyAsync(buyerId, "WELCOME10");

        Assert.True(result.Success);
        Assert.Equal("WELCOME10", result.AppliedPromoCode);
        repo.Verify(r => r.UpsertPromoSelectionAsync(It.IsAny<PromoSelectionModel>()), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_PreventsMultiplePromoCodes()
    {
        var buyerId = "buyer-2";
        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetPromoSelectionAsync(buyerId))
            .ReturnsAsync(new PromoSelectionModel { BuyerId = buyerId, PromoCode = "EXISTING" });

        var service = new PromoService(repo.Object, new GetCartItems(repo.Object), new CartCalculationService(), TimeProvider.System);

        var result = await service.ApplyAsync(buyerId, "NEWCODE");

        Assert.False(result.Success);
        Assert.Contains("one promo code", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        repo.Verify(r => r.UpsertPromoSelectionAsync(It.IsAny<PromoSelectionModel>()), Times.Never);
    }
}
