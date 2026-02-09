namespace SD.ProjectName.Modules.Cart.Application;

using System;
using SD.ProjectName.Modules.Cart.Domain;

public class CartCalculationService
{
    private const decimal DefaultCommissionRate = 0.01m;

    public CartTotals CalculateTotals(
        CartModel cart,
        List<ShippingRuleModel> shippingRules,
        decimal commissionRate = DefaultCommissionRate,
        IReadOnlyDictionary<string, string>? selectedShippingMethods = null)
    {
        var sellerGroups = cart.Items.GroupBy(item => item.SellerId);
        var sellerBreakdown = new List<SellerCartTotals>();
        decimal totalItemsSubtotal = 0;
        decimal totalShipping = 0;

        foreach (var sellerGroup in sellerGroups)
        {
            var sellerId = sellerGroup.Key;
            var sellerItems = sellerGroup.ToList();

            var sellerSubtotal = sellerItems.Sum(item => item.UnitPrice * item.Quantity);
            var totalWeightKg = sellerItems.Sum(item => item.WeightKg * item.Quantity);

            var shippingRule = ResolveShippingRule(shippingRules, sellerId, selectedShippingMethods);
            var shippingCost = CalculateShippingCost(sellerSubtotal, totalWeightKg, shippingRule);

            var totalBeforeCommission = sellerSubtotal + shippingCost;
            var commissionAmount = totalBeforeCommission * commissionRate;
            var sellerPayout = totalBeforeCommission - commissionAmount;

            sellerBreakdown.Add(new SellerCartTotals
            {
                SellerId = sellerId,
                ItemsSubtotal = sellerSubtotal,
                ShippingCost = shippingCost,
                TotalBeforeCommission = totalBeforeCommission,
                CommissionAmount = commissionAmount,
                SellerPayout = sellerPayout
            });

            totalItemsSubtotal += sellerSubtotal;
            totalShipping += shippingCost;
        }

        return new CartTotals
        {
            ItemsSubtotal = totalItemsSubtotal,
            ShippingTotal = totalShipping,
            TotalAmount = totalItemsSubtotal + totalShipping,
            SellerBreakdown = sellerBreakdown
        };
    }

    public PromoDiscountEvaluation EvaluatePromo(CartTotals totals, PromoCodeModel promoCode, DateTimeOffset now)
    {
        if (!promoCode.IsActive)
        {
            return PromoDiscountEvaluation.Failed("This promo code is not active.");
        }

        if (promoCode.ValidFrom.HasValue && now < promoCode.ValidFrom.Value)
        {
            return PromoDiscountEvaluation.Failed("This promo code is not active yet.");
        }

        if (promoCode.ValidUntil.HasValue && now > promoCode.ValidUntil.Value)
        {
            return PromoDiscountEvaluation.Failed("This promo code has expired.");
        }

        var eligibleSubtotal = string.IsNullOrWhiteSpace(promoCode.SellerId)
            ? totals.ItemsSubtotal
            : totals.SellerBreakdown
                .Where(b => string.Equals(b.SellerId, promoCode.SellerId, StringComparison.OrdinalIgnoreCase))
                .Sum(b => b.ItemsSubtotal);

        if (eligibleSubtotal <= 0)
        {
            return PromoDiscountEvaluation.Failed("This promo code does not apply to your cart.");
        }

        if (promoCode.MinimumSubtotal.HasValue && eligibleSubtotal < promoCode.MinimumSubtotal.Value)
        {
            return PromoDiscountEvaluation.Failed($"This promo requires a minimum subtotal of {promoCode.MinimumSubtotal.Value:C}.");
        }

        var rawDiscount = promoCode.DiscountType switch
        {
            PromoDiscountType.Percentage => Math.Round(eligibleSubtotal * promoCode.DiscountValue, 2, MidpointRounding.ToEven),
            PromoDiscountType.FixedAmount => promoCode.DiscountValue,
            _ => 0
        };

        var cappedDiscount = Math.Min(rawDiscount, totals.ItemsSubtotal + totals.ShippingTotal);
        if (cappedDiscount <= 0)
        {
            return PromoDiscountEvaluation.Failed("This promo code does not apply to your cart.");
        }

        return PromoDiscountEvaluation.Success(cappedDiscount);
    }

    public CartTotals ApplyPromo(CartTotals totals, PromoCodeModel promoCode, DateTimeOffset now, PromoDiscountEvaluation? evaluation = null)
    {
        var result = evaluation ?? EvaluatePromo(totals, promoCode, now);
        if (!result.IsEligible)
        {
            return totals;
        }

        totals.DiscountTotal = result.DiscountAmount;
        totals.TotalAmount = totals.ItemsSubtotal + totals.ShippingTotal - totals.DiscountTotal;
        totals.PromoCode = promoCode.Code;
        return totals;
    }

    public decimal CalculateShippingCost(decimal subtotal, decimal totalWeightKg, ShippingRuleModel? shippingRule)
    {
        if (shippingRule == null)
        {
            return 0;
        }

        if (shippingRule.FreeShippingThreshold.HasValue && subtotal >= shippingRule.FreeShippingThreshold.Value)
        {
            return 0;
        }

        decimal shippingCost = shippingRule.BasePrice;

        if (shippingRule.PricePerKg.HasValue && shippingRule.PricePerKg.Value > 0)
        {
            shippingCost += totalWeightKg * shippingRule.PricePerKg.Value;
        }

        return shippingCost;
    }

    private ShippingRuleModel? ResolveShippingRule(
        List<ShippingRuleModel> shippingRules,
        string sellerId,
        IReadOnlyDictionary<string, string>? selectedShippingMethods)
    {
        var sellerRules = shippingRules
            .Where(r => r.SellerId == sellerId && r.IsActive)
            .ToList();

        if (sellerRules.Count == 0)
        {
            return null;
        }

        if (selectedShippingMethods != null && selectedShippingMethods.TryGetValue(sellerId, out var selectedMethod))
        {
            var matched = sellerRules.FirstOrDefault(r =>
                string.Equals(r.ShippingMethod, selectedMethod, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                return matched;
            }
        }

        return sellerRules.First();
    }
}
