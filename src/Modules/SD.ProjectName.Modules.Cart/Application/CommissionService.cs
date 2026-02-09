using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Modules.Cart.Application;

public class CommissionService
{
    private readonly CommissionOptions _options;
    private readonly TimeProvider _timeProvider;

    public CommissionService(IOptions<CommissionOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public void EnsureCommissionCalculated(OrderModel? order)
    {
        if (order is null || order.SubOrders.Count == 0)
        {
            return;
        }

        foreach (var subOrder in order.SubOrders)
        {
            EnsureCommissionCalculated(subOrder);
        }

        order.CommissionTotal = order.SubOrders.Sum(s => s.CommissionAmount);
    }

    public void EnsureCommissionCalculated(SellerOrderModel sellerOrder)
    {
        if (sellerOrder.CommissionCalculatedAt.HasValue)
        {
            return;
        }

        var computation = ComputeCommission(sellerOrder);
        sellerOrder.CommissionRateApplied = computation.AppliedRate;
        sellerOrder.CommissionAmount = computation.CommissionAmount;
        sellerOrder.CommissionCalculatedAt = _timeProvider.GetUtcNow();

        if (sellerOrder.Order is not null)
        {
            sellerOrder.Order.CommissionTotal = sellerOrder.Order.SubOrders.Sum(s => s.CommissionAmount);
        }
    }

    public void RecalculateAfterRefund(SellerOrderModel sellerOrder)
    {
        var effectiveRate = sellerOrder.CommissionCalculatedAt.HasValue
            ? sellerOrder.CommissionRateApplied
            : ComputeCommission(sellerOrder).AppliedRate;

        var netAmount = Math.Max(0m, sellerOrder.TotalAmount - sellerOrder.RefundedAmount);
        sellerOrder.CommissionRateApplied = effectiveRate;
        sellerOrder.CommissionAmount = RoundAmount(netAmount * effectiveRate);
        sellerOrder.CommissionCalculatedAt ??= _timeProvider.GetUtcNow();

        if (sellerOrder.Order is not null)
        {
            sellerOrder.Order.CommissionTotal = sellerOrder.Order.SubOrders.Sum(s => s.CommissionAmount);
        }
    }

    private CommissionComputation ComputeCommission(SellerOrderModel sellerOrder)
    {
        var baseAmount = Math.Max(0m, sellerOrder.TotalAmount);
        var itemsSubtotal = sellerOrder.Items.Sum(i => i.UnitPrice * i.Quantity);

        if (baseAmount == 0)
        {
            var fallbackRate = ResolveRate(sellerOrder.SellerId, null);
            return new CommissionComputation(fallbackRate, 0m);
        }

        decimal commission = 0m;
        foreach (var item in sellerOrder.Items)
        {
            var lineTotal = Math.Max(0m, item.UnitPrice * item.Quantity);
            var rate = ResolveRate(sellerOrder.SellerId, item.Category);
            commission += lineTotal * rate;
        }

        var remaining = baseAmount - itemsSubtotal;
        if (remaining != 0)
        {
            var remainderRate = ResolveRate(sellerOrder.SellerId, null);
            commission += remaining * remainderRate;
        }

        var appliedRate = commission / baseAmount;
        return new CommissionComputation(RoundRate(appliedRate), RoundAmount(commission));
    }

    private decimal ResolveRate(string sellerId, string? category)
    {
        if (!string.IsNullOrWhiteSpace(sellerId) &&
            _options.SellerOverrides.TryGetValue(sellerId, out var sellerRate))
        {
            return sellerRate;
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalized = category.Trim();
            if (_options.CategoryOverrides.TryGetValue(normalized, out var categoryRate))
            {
                return categoryRate;
            }
        }

        return _options.DefaultRate;
    }

    private static decimal RoundAmount(decimal value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private record CommissionComputation(decimal AppliedRate, decimal CommissionAmount);
}
