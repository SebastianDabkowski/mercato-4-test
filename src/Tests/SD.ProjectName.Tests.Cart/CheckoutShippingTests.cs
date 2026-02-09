using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Pages.Buyer.Checkout;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.Tests.Cart;

public class CheckoutShippingTests
{
    [Fact]
    public async Task OnPostAsync_SavesPostedShippingSelection()
    {
        var buyerId = "buyer-1";
        var cartItems = new List<CartItemModel>
        {
            new()
            {
                ProductId = 1,
                ProductName = "Sample",
                SellerId = "seller-1",
                SellerName = "Seller One",
                UnitPrice = 20m,
                Quantity = 1,
                WeightKg = 1m
            }
        };

        var repo = new Mock<ICartRepository>();
        repo.Setup(r => r.GetByBuyerIdAsync(buyerId)).ReturnsAsync(cartItems);
        repo.Setup(r => r.GetSelectedAddressAsync(buyerId)).ReturnsAsync(new DeliveryAddressModel
        {
            BuyerId = buyerId,
            RecipientName = "Buyer",
            Line1 = "123 Main St",
            City = "City",
            Region = "Region",
            PostalCode = "12345",
            CountryCode = "US"
        });
        repo.Setup(r => r.GetShippingSelectionsAsync(buyerId)).ReturnsAsync(new List<ShippingSelectionModel>
        {
            new()
            {
                BuyerId = buyerId,
                SellerId = "seller-1",
                ShippingMethod = "Standard",
                Cost = 5m
            }
        });
        repo.Setup(r => r.GetShippingRulesAsync()).ReturnsAsync(new List<ShippingRuleModel>
        {
            new() { SellerId = "seller-1", ShippingMethod = "Standard", BasePrice = 5m, DeliveryEstimate = "3-5 days", IsActive = true },
            new() { SellerId = "seller-1", ShippingMethod = "Express", BasePrice = 12m, DeliveryEstimate = "1-2 days", IsActive = true }
        });
        repo.Setup(r => r.ClearPaymentSelectionAsync(buyerId)).Returns(Task.CompletedTask);
        repo.Setup(r => r.GetPromoSelectionAsync(buyerId)).ReturnsAsync((PromoSelectionModel?)null);
        repo.Setup(r => r.GetPromoCodeAsync(It.IsAny<string>())).ReturnsAsync((PromoCodeModel?)null);

        string? capturedMethod = null;
        decimal capturedCost = 0;
        string? capturedEstimate = null;
        repo.Setup(r => r.SetShippingSelectionAsync(buyerId, "seller-1", It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string?>()))
            .Callback<string, string, string, decimal, string?>((_, _, method, cost, estimate) =>
            {
                capturedMethod = method;
                capturedCost = cost;
                capturedEstimate = estimate;
            })
            .Returns(Task.CompletedTask);

        var identity = new Mock<ICartIdentityService>();
        identity.Setup(i => i.GetOrCreateBuyerId()).Returns(buyerId);

        var cartCalculation = new CartCalculationService();
        var getCartItems = new GetCartItems(repo.Object);
        var promoService = new PromoService(repo.Object, getCartItems, cartCalculation, TimeProvider.System);

        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());

        var page = new ShippingModel(identity.Object, getCartItems, repo.Object, cartCalculation, promoService)
        {
            PageContext = new PageContext(actionContext),
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
            Selections = new List<SellerShippingSelectionInput>
            {
                new() { SellerId = "seller-1", ShippingMethod = "Express" }
            }
        };

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Buyer/Checkout/Payment", redirect.PageName);
        Assert.Equal("Express", capturedMethod);
        Assert.Equal(12m, capturedCost);
        Assert.Equal("1-2 days", capturedEstimate);
        repo.Verify(r => r.ClearPaymentSelectionAsync(buyerId), Times.Once);
    }
}
