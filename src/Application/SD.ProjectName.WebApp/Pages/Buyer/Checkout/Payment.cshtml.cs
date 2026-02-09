using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Services;
using Microsoft.Extensions.Options;
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
    private readonly PaymentOptions _paymentOptions;
    private readonly TimeProvider _timeProvider;

    public PaymentModel(
        ICartIdentityService cartIdentityService,
        GetCartItems getCartItems,
        ICartRepository cartRepository,
        CartCalculationService cartCalculationService,
        PromoService promoService,
        IOptions<PaymentOptions> paymentOptions,
        TimeProvider timeProvider)
    {
        _cartIdentityService = cartIdentityService;
        _getCartItems = getCartItems;
        _cartRepository = cartRepository;
        _cartCalculationService = cartCalculationService;
        _promoService = promoService;
        _paymentOptions = paymentOptions.Value;
        _timeProvider = timeProvider;
    }

    [BindProperty]
    public string SelectedPaymentMethod { get; set; } = string.Empty;
    [BindProperty]
    public string? BlikCode { get; set; }

    public CartTotals Totals { get; private set; } = new();
    public DeliveryAddressModel? SelectedAddress { get; private set; }
    public PaymentSelectionModel? CurrentPaymentSelection { get; private set; }
    public List<ShippingSelectionModel> ShippingSelections { get; private set; } = new();
    public string? PromoError { get; private set; }
    public string? PromoSuccess { get; private set; }
    public IEnumerable<string> AvailablePaymentMethods { get; private set; } = Array.Empty<string>();

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
            !AvailablePaymentMethods.Contains(SelectedPaymentMethod, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Select a valid payment method.");
        }

        if (string.Equals(SelectedPaymentMethod, "BLIK", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(BlikCode) || BlikCode.Trim().Length != 6 || !BlikCode.All(char.IsDigit))
            {
                ModelState.AddModelError(nameof(BlikCode), "Enter a valid 6-digit BLIK code.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var providerReference = $"p24_{Guid.NewGuid():N}";
        var selection = new PaymentSelectionModel
        {
            BuyerId = buyerId,
            PaymentMethod = SelectedPaymentMethod,
            Status = PaymentStatus.Pending,
            ProviderReference = providerReference,
            UpdatedAt = _timeProvider.GetUtcNow(),
            OrderId = null
        };

        selection = await _cartRepository.UpsertPaymentSelectionAsync(selection);

        var redirectUrl = Url.Page("/Payments/ProviderRedirect", new
        {
            paymentReference = selection.ProviderReference,
            method = selection.PaymentMethod,
            blikCode = BlikCode
        });

        if (redirectUrl is null)
        {
            TempData["PaymentError"] = "Unable to start payment with the provider.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        return Redirect(redirectUrl);
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

        AvailablePaymentMethods = ResolveEnabledMethods();
        CurrentPaymentSelection = await _cartRepository.GetPaymentSelectionAsync(buyerId);
        if (setSelectedPaymentMethodFromExisting)
        {
            if (!string.IsNullOrWhiteSpace(CurrentPaymentSelection?.PaymentMethod) &&
                AvailablePaymentMethods.Contains(CurrentPaymentSelection.PaymentMethod, StringComparer.OrdinalIgnoreCase))
            {
                SelectedPaymentMethod = CurrentPaymentSelection.PaymentMethod;
            }
            else if (string.IsNullOrWhiteSpace(SelectedPaymentMethod))
            {
                SelectedPaymentMethod = AvailablePaymentMethods.FirstOrDefault() ?? string.Empty;
            }
        }
        else if (!string.IsNullOrWhiteSpace(SelectedPaymentMethod) &&
                 !AvailablePaymentMethods.Contains(SelectedPaymentMethod, StringComparer.OrdinalIgnoreCase))
        {
            SelectedPaymentMethod = AvailablePaymentMethods.FirstOrDefault() ?? string.Empty;
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

    private string[] ResolveEnabledMethods()
    {
        var configured = _paymentOptions.EnabledMethods ?? Array.Empty<string>();
        var normalizedConfigured = configured
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToArray();

        return normalizedConfigured.Length > 0
            ? normalizedConfigured
            : PaymentMethods.Supported;
    }

    private static bool HasSelectionsForAllSellers(IEnumerable<CartItemModel> cartItems, IEnumerable<ShippingSelectionModel> selections)
    {
        var sellerIds = cartItems.Select(i => i.SellerId).Distinct().ToList();
        return sellerIds.All(id => selections.Any(s => s.SellerId == id));
    }
}

public static class PaymentMethods
{
    public static readonly string[] Supported = new[] { "Przelewy24", "Card", "BankTransfer", "BLIK" };
}
