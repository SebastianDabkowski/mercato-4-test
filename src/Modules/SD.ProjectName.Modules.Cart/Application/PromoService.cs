namespace SD.ProjectName.Modules.Cart.Application;

using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

public class PromoService
{
    private readonly ICartRepository _cartRepository;
    private readonly GetCartItems _getCartItems;
    private readonly CartCalculationService _cartCalculationService;
    private readonly TimeProvider _timeProvider;

    public PromoService(
        ICartRepository cartRepository,
        GetCartItems getCartItems,
        CartCalculationService cartCalculationService,
        TimeProvider timeProvider)
    {
        _cartRepository = cartRepository;
        _getCartItems = getCartItems;
        _cartCalculationService = cartCalculationService;
        _timeProvider = timeProvider;
    }

    public async Task<PromoApplyResult> ApplyAsync(
        string buyerId,
        string promoCode,
        IReadOnlyDictionary<string, string>? selectedShippingMethods = null)
    {
        if (string.IsNullOrWhiteSpace(promoCode))
        {
            return PromoApplyResult.Failed("Enter a promo code.");
        }

        var normalizedCode = promoCode.Trim().ToUpperInvariant();
        var existingSelection = await _cartRepository.GetPromoSelectionAsync(buyerId);
        if (existingSelection is not null &&
            !string.Equals(existingSelection.PromoCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
        {
            return PromoApplyResult.Failed("Only one promo code can be applied at a time. Remove the current code to try another.");
        }

        var cartItems = await _getCartItems.ExecuteAsync(buyerId);
        if (!cartItems.Any())
        {
            return PromoApplyResult.Failed("Add items to your cart before applying a promo code.");
        }

        var promo = await _cartRepository.GetPromoCodeAsync(normalizedCode);
        if (promo is null)
        {
            return PromoApplyResult.Failed("Invalid or expired promo code.");
        }

        var shippingRules = await _cartRepository.GetShippingRulesAsync();
        var baseTotals = _cartCalculationService.CalculateTotals(
            new CartModel { Items = cartItems },
            shippingRules,
            selectedShippingMethods: selectedShippingMethods);

        var evaluation = _cartCalculationService.EvaluatePromo(baseTotals, promo, _timeProvider.GetUtcNow());
        if (!evaluation.IsEligible)
        {
            return PromoApplyResult.Failed(evaluation.FailureReason ?? "This promo code does not apply to your cart.");
        }

        await _cartRepository.UpsertPromoSelectionAsync(new PromoSelectionModel
        {
            BuyerId = buyerId,
            PromoCode = promo.Code,
            AppliedAt = _timeProvider.GetUtcNow()
        });

        return PromoApplyResult.SuccessResult(promo.Code);
    }

    public async Task<PromoTotalsResult> ApplyExistingAsync(string buyerId, CartTotals totals)
    {
        var selection = await _cartRepository.GetPromoSelectionAsync(buyerId);
        if (selection is null)
        {
            return PromoTotalsResult.WithoutPromo(totals, null);
        }

        var promo = await _cartRepository.GetPromoCodeAsync(selection.PromoCode);
        if (promo is null)
        {
            await _cartRepository.ClearPromoSelectionAsync(buyerId);
            return PromoTotalsResult.WithoutPromo(totals, "Promo code is invalid or no longer available.");
        }

        var evaluation = _cartCalculationService.EvaluatePromo(totals, promo, _timeProvider.GetUtcNow());
        if (!evaluation.IsEligible)
        {
            await _cartRepository.ClearPromoSelectionAsync(buyerId);
            return PromoTotalsResult.WithoutPromo(totals, evaluation.FailureReason ?? "Promo code no longer applies to your cart.");
        }

        var updatedTotals = _cartCalculationService.ApplyPromo(totals, promo, _timeProvider.GetUtcNow(), evaluation);
        return PromoTotalsResult.WithPromo(updatedTotals, promo.Code, evaluation.DiscountAmount);
    }

    public Task ClearAsync(string buyerId) => _cartRepository.ClearPromoSelectionAsync(buyerId);
}

public record PromoApplyResult(bool Success, string? ErrorMessage, string? AppliedPromoCode)
{
    public static PromoApplyResult Failed(string message) => new(false, message, null);
    public static PromoApplyResult SuccessResult(string promoCode) => new(true, null, promoCode);
}

public record PromoTotalsResult(CartTotals Totals, string? AppliedPromoCode, string? ErrorMessage, decimal DiscountAmount)
{
    public bool HasPromo => !string.IsNullOrWhiteSpace(AppliedPromoCode);

    public static PromoTotalsResult WithPromo(CartTotals totals, string promoCode, decimal discountAmount) =>
        new(totals, promoCode, null, discountAmount);

    public static PromoTotalsResult WithoutPromo(CartTotals totals, string? errorMessage) =>
        new(totals, null, errorMessage, 0);
}
