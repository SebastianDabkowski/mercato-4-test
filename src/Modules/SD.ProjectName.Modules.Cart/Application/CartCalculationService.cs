namespace SD.ProjectName.Modules.Cart.Application;

using SD.ProjectName.Modules.Cart.Domain;

public class CartCalculationService
{
    private const decimal DefaultCommissionRate = 0.01m;

    public CartTotals CalculateTotals(CartModel cart, List<ShippingRuleModel> shippingRules, decimal commissionRate = DefaultCommissionRate)
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

            var shippingRule = shippingRules.FirstOrDefault(r => r.SellerId == sellerId && r.IsActive);
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

    private decimal CalculateShippingCost(decimal subtotal, decimal totalWeightKg, ShippingRuleModel? shippingRule)
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
}
