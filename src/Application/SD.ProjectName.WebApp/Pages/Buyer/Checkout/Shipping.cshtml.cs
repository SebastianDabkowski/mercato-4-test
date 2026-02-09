using System;
using System.ComponentModel.DataAnnotations;
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
public class ShippingModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly GetCartItems _getCartItems;
    private readonly ICartRepository _cartRepository;
    private readonly CartCalculationService _cartCalculationService;
    private readonly PromoService _promoService;

    public ShippingModel(
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

    public List<SellerShippingOptionsViewModel> SellerOptions { get; private set; } = new();
    public CartTotals Totals { get; private set; } = new();
    public DeliveryAddressModel? SelectedAddress { get; private set; }
    public string? PromoError { get; private set; }
    public string? PromoSuccess { get; private set; }

    [BindProperty]
    public List<SellerShippingSelectionInput> Selections { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        PromoError = TempData["PromoError"] as string;
        PromoSuccess = TempData["PromoSuccess"] as string;
        return await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        PromoError = TempData["PromoError"] as string;
        PromoSuccess = TempData["PromoSuccess"] as string;
        var result = await LoadAsync();
        if (result is RedirectToPageResult)
        {
            return result;
        }

        if (!SellerOptions.Any())
        {
            ModelState.AddModelError(string.Empty, "No shipping options are available for your cart.");
        }

        foreach (var sellerOption in SellerOptions)
        {
            var selection = Selections.FirstOrDefault(s => s.SellerId == sellerOption.SellerId);
            if (selection is null || string.IsNullOrWhiteSpace(selection.ShippingMethod))
            {
                ModelState.AddModelError(string.Empty, $"Select a shipping method for {sellerOption.SellerLabel}.");
                continue;
            }

            if (!sellerOption.Options.Any(o => string.Equals(o.ShippingMethod, selection.ShippingMethod, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(string.Empty, $"Selected shipping method is not available for {sellerOption.SellerLabel}.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        foreach (var selection in Selections)
        {
            var sellerOption = SellerOptions.First(o => o.SellerId == selection.SellerId);
            var selectedOption = sellerOption.Options.First(o => string.Equals(o.ShippingMethod, selection.ShippingMethod, StringComparison.OrdinalIgnoreCase));
            await _cartRepository.SetShippingSelectionAsync(buyerId, sellerOption.SellerId, selectedOption.ShippingMethod, selectedOption.Cost);
        }

        await _cartRepository.ClearPaymentSelectionAsync(buyerId);
        TempData["ShippingSaved"] = "Shipping methods selected.";
        return RedirectToPage("/Buyer/Checkout/Payment");
    }

    public async Task<IActionResult> OnPostApplyPromoAsync(string promoCode)
    {
        var loadResult = await LoadAsync();
        if (loadResult is RedirectToPageResult)
        {
            return loadResult;
        }

        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var selectionMap = GetSelectionMap();
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

    private async Task<IActionResult> LoadAsync()
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
            TempData["CheckoutError"] = "Select a delivery address before choosing shipping.";
            return RedirectToPage("/Buyer/Checkout/Address");
        }

        var shippingRules = await _cartRepository.GetShippingRulesAsync();
        var existingSelections = await _cartRepository.GetShippingSelectionsAsync(buyerId);
        SellerOptions = BuildSellerOptions(cartItems, shippingRules, existingSelections);
        Selections = SellerOptions
            .Select(o => new SellerShippingSelectionInput
            {
                SellerId = o.SellerId,
                ShippingMethod = o.SelectedShippingMethod ?? string.Empty
            })
            .ToList();

        Totals = BuildTotals(cartItems, shippingRules, SellerOptions);
        var promoTotals = await _promoService.ApplyExistingAsync(buyerId, Totals);
        if (!promoTotals.HasPromo && promoTotals.ErrorMessage is not null)
        {
            PromoError ??= promoTotals.ErrorMessage;
        }

        Totals = promoTotals.Totals;
        return Page();
    }

    private List<SellerShippingOptionsViewModel> BuildSellerOptions(
        List<CartItemModel> cartItems,
        List<ShippingRuleModel> shippingRules,
        List<ShippingSelectionModel> existingSelections)
    {
        var result = new List<SellerShippingOptionsViewModel>();
        var groupedItems = cartItems.GroupBy(i => new { i.SellerId, i.SellerName });

        foreach (var group in groupedItems)
        {
            var sellerItems = group.ToList();
            var subtotal = sellerItems.Sum(i => i.UnitPrice * i.Quantity);
            var totalWeight = sellerItems.Sum(i => i.WeightKg * i.Quantity);

            var applicableRules = shippingRules
                .Where(r => r.SellerId == group.Key.SellerId && r.IsActive)
                .Where(r => !r.MaxWeightKg.HasValue || totalWeight <= r.MaxWeightKg.Value)
                .ToList();

            var options = applicableRules
                .Select(r => new ShippingOptionViewModel(
                    r.ShippingMethod,
                    _cartCalculationService.CalculateShippingCost(subtotal, totalWeight, r)))
                .OrderBy(o => o.Cost)
                .ToList();

            var selectedMethod = existingSelections.FirstOrDefault(s => s.SellerId == group.Key.SellerId)?.ShippingMethod;
            if (selectedMethod is null || !options.Any(o => string.Equals(o.ShippingMethod, selectedMethod, StringComparison.OrdinalIgnoreCase)))
            {
                selectedMethod = options.FirstOrDefault()?.ShippingMethod;
            }

            result.Add(new SellerShippingOptionsViewModel(
                group.Key.SellerId,
                group.Key.SellerName,
                options,
                selectedMethod));
        }

        return result;
    }

    private CartTotals BuildTotals(
        List<CartItemModel> cartItems,
        List<ShippingRuleModel> shippingRules,
        List<SellerShippingOptionsViewModel> sellerOptions)
    {
        var selectionMap = sellerOptions
            .Where(o => !string.IsNullOrWhiteSpace(o.SelectedShippingMethod))
            .ToDictionary(o => o.SellerId, o => o.SelectedShippingMethod!, StringComparer.OrdinalIgnoreCase);

        var cart = new CartDomainModel { Items = cartItems };
        return _cartCalculationService.CalculateTotals(
            cart,
            shippingRules,
            selectedShippingMethods: selectionMap);
    }

    private Dictionary<string, string> GetSelectionMap()
    {
        return SellerOptions
            .Where(o => !string.IsNullOrWhiteSpace(o.SelectedShippingMethod))
            .ToDictionary(o => o.SellerId, o => o.SelectedShippingMethod!, StringComparer.OrdinalIgnoreCase);
    }
}

public record SellerShippingSelectionInput
{
    [Required]
    public string SellerId { get; set; } = string.Empty;

    [Required]
    public string ShippingMethod { get; set; } = string.Empty;
}

public record SellerShippingOptionsViewModel(
    string SellerId,
    string SellerName,
    List<ShippingOptionViewModel> Options,
    string? SelectedShippingMethod)
{
    public string SellerLabel => string.IsNullOrWhiteSpace(SellerName) ? SellerId : SellerName;
}

public record ShippingOptionViewModel(string ShippingMethod, decimal Cost);
