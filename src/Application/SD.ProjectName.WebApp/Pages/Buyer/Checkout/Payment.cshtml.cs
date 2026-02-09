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
public class PaymentModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly GetCartItems _getCartItems;
    private readonly ICartRepository _cartRepository;
    private readonly CartCalculationService _cartCalculationService;
    private readonly PromoService _promoService;

    public PaymentModel(
        ICartIdentityService cartIdentityService,
        GetCartItems getCartItems,
        ICartRepository cartRepository,
        CartCalculationService cartCalculationService,
        PromoService promoService)
    {
        _cartIdentityService = cartIdentityService;
        _getCartItems = getCartItems;
        _cartRepository = cartRepository;
        _cartCalculationService = cartCalculationService;
        _promoService = promoService;
    }

    [BindProperty]
    public string SelectedPaymentMethod { get; set; } = string.Empty;

    public CartTotals Totals { get; private set; } = new();
    public DeliveryAddressModel? SelectedAddress { get; private set; }
    public PaymentSelectionModel? CurrentPaymentSelection { get; private set; }
    public List<ShippingSelectionModel> ShippingSelections { get; private set; } = new();
    public string? PromoError { get; private set; }
    public string? PromoSuccess { get; private set; }
    public IEnumerable<string> AvailablePaymentMethods => PaymentMethods.Supported;

    public async Task<IActionResult> OnGetAsync()
    {
        PromoError = TempData["PromoError"] as string;
        PromoSuccess = TempData["PromoSuccess"] as string;
        return await LoadCheckoutStateAsync(setSelectedPaymentMethodFromExisting: true);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        PromoError = TempData["PromoError"] as string;
        PromoSuccess = TempData["PromoSuccess"] as string;
        var loadResult = await LoadCheckoutStateAsync(setSelectedPaymentMethodFromExisting: false);
        if (loadResult is RedirectToPageResult)
        {
            return loadResult;
        }

        if (string.IsNullOrWhiteSpace(SelectedPaymentMethod) ||
            !PaymentMethods.Supported.Contains(SelectedPaymentMethod, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Select a valid payment method.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var selection = new PaymentSelectionModel
        {
            BuyerId = buyerId,
            PaymentMethod = SelectedPaymentMethod,
            Status = PaymentStatus.Authorized,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _cartRepository.UpsertPaymentSelectionAsync(selection);
        TempData["PaymentStatus"] = "Payment authorized successfully.";
        return RedirectToPage("/Buyer/Checkout/Confirmation");
    }

    public async Task<IActionResult> OnPostApplyPromoAsync(string promoCode)
    {
        var loadResult = await LoadCheckoutStateAsync(setSelectedPaymentMethodFromExisting: true);
        if (loadResult is RedirectToPageResult)
        {
            return loadResult;
        }

        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var selectionMap = ShippingSelections.ToDictionary(s => s.SellerId, s => s.ShippingMethod, StringComparer.OrdinalIgnoreCase);
        var result = await _promoService.ApplyAsync(buyerId, promoCode, selectionMap);
        if (result.Success)
        {
            TempData["PromoSuccess"] = $"Promo code {result.AppliedPromoCode} applied.";
        }
        else
        {
            TempData["PromoError"] = result.ErrorMessage ?? "Unable to apply promo code.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearPromoAsync()
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        await _promoService.ClearAsync(buyerId);
        TempData["PromoSuccess"] = "Promo code removed.";
        return RedirectToPage();
    }

    private async Task<IActionResult> LoadCheckoutStateAsync(bool setSelectedPaymentMethodFromExisting)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var cartItems = await _getCartItems.ExecuteAsync(buyerId);
        if (!cartItems.Any())
        {
            return RedirectToPage("/Buyer/Cart");
        }

        SelectedAddress = await _cartRepository.GetSelectedAddressAsync(buyerId);
        if (SelectedAddress is null)
        {
            TempData["CheckoutError"] = "Select a delivery address before choosing payment.";
            return RedirectToPage("/Buyer/Checkout/Address");
        }

        ShippingSelections = await _cartRepository.GetShippingSelectionsAsync(buyerId);
        var shippingRules = await _cartRepository.GetShippingRulesAsync();

        if (!HasSelectionsForAllSellers(cartItems, ShippingSelections))
        {
            TempData["CheckoutError"] = "Choose shipping methods for all sellers before payment.";
            return RedirectToPage("/Buyer/Checkout/Shipping");
        }

        Totals = BuildTotals(cartItems, shippingRules, ShippingSelections);
        var promoTotals = await _promoService.ApplyExistingAsync(buyerId, Totals);
        if (!promoTotals.HasPromo && promoTotals.ErrorMessage is not null)
        {
            PromoError ??= promoTotals.ErrorMessage;
        }

        Totals = promoTotals.Totals;

        CurrentPaymentSelection = await _cartRepository.GetPaymentSelectionAsync(buyerId);
        if (setSelectedPaymentMethodFromExisting)
        {
            if (!string.IsNullOrWhiteSpace(CurrentPaymentSelection?.PaymentMethod))
            {
                SelectedPaymentMethod = CurrentPaymentSelection.PaymentMethod;
            }
            else if (string.IsNullOrWhiteSpace(SelectedPaymentMethod))
            {
                SelectedPaymentMethod = PaymentMethods.Supported.First();
            }
        }

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

public static class PaymentMethods
{
    public static readonly string[] Supported = new[] { "Przelewy24", "Card", "BankTransfer" };
}
