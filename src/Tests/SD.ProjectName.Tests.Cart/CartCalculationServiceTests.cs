using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Tests.Cart;

public class CartCalculationServiceTests
{
    [Fact]
    public void CalculateTotals_SingleSeller_ReturnsCorrectTotals()
    {
        var service = new CartCalculationService();
        var cart = new CartModel
        {
            Id = 1,
            UserId = "user1",
            Items = new List<CartItemModel>
            {
                new CartItemModel { ProductId = 1, SellerId = "seller1", UnitPrice = 100, Quantity = 2, WeightKg = 1 },
                new CartItemModel { ProductId = 2, SellerId = "seller1", UnitPrice = 50, Quantity = 1, WeightKg = 0.5m }
            }
        };

        var shippingRules = new List<ShippingRuleModel>
        {
            new ShippingRuleModel { SellerId = "seller1", BasePrice = 10, IsActive = true }
        };

        var result = service.CalculateTotals(cart, shippingRules, 0.01m);

        Assert.Equal(250, result.ItemsSubtotal);
        Assert.Equal(10, result.ShippingTotal);
        Assert.Equal(260, result.TotalAmount);
        Assert.Single(result.SellerBreakdown);
        Assert.Equal(250, result.SellerBreakdown[0].ItemsSubtotal);
        Assert.Equal(10, result.SellerBreakdown[0].ShippingCost);
        Assert.Equal(260, result.SellerBreakdown[0].TotalBeforeCommission);
        Assert.Equal(2.60m, result.SellerBreakdown[0].CommissionAmount);
        Assert.Equal(257.40m, result.SellerBreakdown[0].SellerPayout);
    }

    [Fact]
    public void CalculateTotals_MultipleSellers_ReturnsCorrectTotals()
    {
        var service = new CartCalculationService();
        var cart = new CartModel
        {
            Id = 1,
            UserId = "user1",
            Items = new List<CartItemModel>
            {
                new CartItemModel { ProductId = 1, SellerId = "seller1", UnitPrice = 100, Quantity = 2, WeightKg = 1 },
                new CartItemModel { ProductId = 2, SellerId = "seller2", UnitPrice = 50, Quantity = 1, WeightKg = 0.5m }
            }
        };

        var shippingRules = new List<ShippingRuleModel>
        {
            new ShippingRuleModel { SellerId = "seller1", BasePrice = 10, IsActive = true },
            new ShippingRuleModel { SellerId = "seller2", BasePrice = 15, IsActive = true }
        };

        var result = service.CalculateTotals(cart, shippingRules, 0.01m);

        Assert.Equal(250, result.ItemsSubtotal);
        Assert.Equal(25, result.ShippingTotal);
        Assert.Equal(275, result.TotalAmount);
        Assert.Equal(2, result.SellerBreakdown.Count);
    }

    [Fact]
    public void CalculateTotals_FreeShippingThreshold_AppliesFreeShipping()
    {
        var service = new CartCalculationService();
        var cart = new CartModel
        {
            Id = 1,
            UserId = "user1",
            Items = new List<CartItemModel>
            {
                new CartItemModel { ProductId = 1, SellerId = "seller1", UnitPrice = 100, Quantity = 2, WeightKg = 1 }
            }
        };

        var shippingRules = new List<ShippingRuleModel>
        {
            new ShippingRuleModel { SellerId = "seller1", BasePrice = 10, FreeShippingThreshold = 150, IsActive = true }
        };

        var result = service.CalculateTotals(cart, shippingRules, 0.01m);

        Assert.Equal(200, result.ItemsSubtotal);
        Assert.Equal(0, result.ShippingTotal);
        Assert.Equal(200, result.TotalAmount);
    }

    [Fact]
    public void CalculateTotals_WeightBasedShipping_CalculatesCorrectly()
    {
        var service = new CartCalculationService();
        var cart = new CartModel
        {
            Id = 1,
            UserId = "user1",
            Items = new List<CartItemModel>
            {
                new CartItemModel { ProductId = 1, SellerId = "seller1", UnitPrice = 50, Quantity = 2, WeightKg = 2 }
            }
        };

        var shippingRules = new List<ShippingRuleModel>
        {
            new ShippingRuleModel { SellerId = "seller1", BasePrice = 10, PricePerKg = 5, IsActive = true }
        };

        var result = service.CalculateTotals(cart, shippingRules, 0.01m);

        Assert.Equal(100, result.ItemsSubtotal);
        Assert.Equal(30, result.ShippingTotal); // 10 base + (4kg * 5)
        Assert.Equal(130, result.TotalAmount);
    }

    [Fact]
    public void CalculateTotals_NoShippingRule_ChargesZeroShipping()
    {
        var service = new CartCalculationService();
        var cart = new CartModel
        {
            Id = 1,
            UserId = "user1",
            Items = new List<CartItemModel>
            {
                new CartItemModel { ProductId = 1, SellerId = "seller1", UnitPrice = 100, Quantity = 1, WeightKg = 1 }
            }
        };

        var shippingRules = new List<ShippingRuleModel>();

        var result = service.CalculateTotals(cart, shippingRules, 0.01m);

        Assert.Equal(100, result.ItemsSubtotal);
        Assert.Equal(0, result.ShippingTotal);
        Assert.Equal(100, result.TotalAmount);
    }

    [Fact]
    public void CalculateTotals_EmptyCart_ReturnsZeroTotals()
    {
        var service = new CartCalculationService();
        var cart = new CartModel
        {
            Id = 1,
            UserId = "user1",
            Items = new List<CartItemModel>()
        };

        var shippingRules = new List<ShippingRuleModel>();

        var result = service.CalculateTotals(cart, shippingRules, 0.01m);

        Assert.Equal(0, result.ItemsSubtotal);
        Assert.Equal(0, result.ShippingTotal);
        Assert.Equal(0, result.TotalAmount);
        Assert.Empty(result.SellerBreakdown);
    }
}
