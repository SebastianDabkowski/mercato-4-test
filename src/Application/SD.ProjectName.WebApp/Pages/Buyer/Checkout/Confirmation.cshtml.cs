using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Services;
using CartDomainModel = SD.ProjectName.Modules.Cart.Domain.CartModel;

namespace SD.ProjectName.WebApp.Pages.Buyer.Checkout;

[AllowAnonymous]
public class ConfirmationModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly GetCartItems _getCartItems;
    private readonly ICartRepository _cartRepository;
    private readonly CartCalculationService _cartCalculationService;

    public ConfirmationModel(
        ICartIdentityService cartIdentityService,
        GetCartItems getCartItems,
        ICartRepository cartRepository,
        CartCalculationService cartCalculationService)
    {
        _cartIdentityService = cartIdentityService;
        _getCartItems = getCartItems;
        _cartRepository = cartRepository;
        _cartCalculationService = cartCalculationService;
    }

    public CartTotals Totals { get; private set; } = new();
    public DeliveryAddressModel? SelectedAddress { get; private set; }
    public PaymentSelectionModel? PaymentSelection { get; private set; }
    public List<ShippingSelectionModel> ShippingSelections { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var cartItems = await _getCartItems.ExecuteAsync(buyerId);
        if (!cartItems.Any())
        {
            return RedirectToPage("/Buyer/Cart");
        }

        PaymentSelection = await _cartRepository.GetPaymentSelectionAsync(buyerId);
        if (PaymentSelection is null || PaymentSelection.Status != PaymentStatus.Authorized)
        {
            TempData["CheckoutError"] = "Complete payment before viewing the confirmation page.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        SelectedAddress = await _cartRepository.GetSelectedAddressAsync(buyerId);
        if (SelectedAddress is null)
        {
            TempData["CheckoutError"] = "Select a delivery address before completing checkout.";
            return RedirectToPage("/Buyer/Checkout/Address");
        }

        ShippingSelections = await _cartRepository.GetShippingSelectionsAsync(buyerId);
        if (!HasSelectionsForAllSellers(cartItems, ShippingSelections))
        {
            TempData["CheckoutError"] = "Choose shipping methods for all sellers before completing checkout.";
            return RedirectToPage("/Buyer/Checkout/Shipping");
        }

        var shippingRules = await _cartRepository.GetShippingRulesAsync();
        Totals = BuildTotals(cartItems, shippingRules, ShippingSelections);

        return Page();
    }

    private CartTotals BuildTotals(
        List<CartItemModel> cartItems,
        List<ShippingRuleModel> shippingRules,
        List<ShippingSelectionModel> shippingSelections)
    {
        var selectionMap = shippingSelections.ToDictionary(
            s => s.SellerId,
            s => s.ShippingMethod,
            StringComparer.OrdinalIgnoreCase);

        var cart = new CartDomainModel { Items = cartItems };
        return _cartCalculationService.CalculateTotals(
            cart,
            shippingRules,
            selectedShippingMethods: selectionMap);
    }

    private static bool HasSelectionsForAllSellers(IEnumerable<CartItemModel> cartItems, IEnumerable<ShippingSelectionModel> selections)
    {
        var sellerIds = cartItems.Select(i => i.SellerId).Distinct().ToList();
        return sellerIds.All(id => selections.Any(s => s.SellerId == id));
    }
}
