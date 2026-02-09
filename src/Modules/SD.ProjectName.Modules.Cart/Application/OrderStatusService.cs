using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class OrderStatusService
{
    private readonly ICartRepository _cartRepository;
    private readonly EscrowService _escrowService;
    private readonly CommissionService _commissionService;
    private readonly TimeProvider _timeProvider;

    public OrderStatusService(
        ICartRepository cartRepository,
        EscrowService escrowService,
        CommissionService commissionService,
        TimeProvider timeProvider)
    {
        _cartRepository = cartRepository;
        _escrowService = escrowService;
        _commissionService = commissionService;
        _timeProvider = timeProvider;
    }

    public async Task<OrderStatusResult> UpdateSellerOrderStatusAsync(
        int sellerOrderId,
        string sellerId,
        string targetStatus,
        string? trackingNumber = null,
        string? trackingCarrier = null,
        string? trackingUrl = null,
        decimal? refundAmount = null)
    {
        var sellerOrder = await _cartRepository.GetSellerOrderAsync(sellerOrderId, sellerId);
        if (sellerOrder is null)
        {
            return OrderStatusResult.NotFound("Sub-order not found.");
        }

        var previousStatus = OrderStatusFlow.NormalizeStatus(sellerOrder.Status);
        var previousTrackingNumber = sellerOrder.TrackingNumber;
        var previousTrackingCarrier = sellerOrder.TrackingCarrier;
        var previousTrackingUrl = sellerOrder.TrackingUrl;
        if (!OrderStatusFlow.IsValidTransition(sellerOrder.Status, targetStatus))
        {
            return OrderStatusResult.InvalidTransition(sellerOrder.Status, targetStatus);
        }

        if (string.Equals(targetStatus, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase))
        {
            var amount = refundAmount ?? sellerOrder.TotalAmount;
            return await RefundSellerOrderInternalAsync(
                sellerOrder,
                amount,
                "Seller marked order refunded",
                overrideReturnRules: true);
        }

        ApplyStatusChange(sellerOrder, targetStatus, trackingNumber, trackingCarrier, trackingUrl, refundAmount);
        var refundOverride = string.Equals(targetStatus, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase)
            ? refundAmount
            : null;

        RecalculateSellerOrderFromItems(sellerOrder, refundOverride);
        RollupOrderStatus(sellerOrder.Order);
        _commissionService.RecalculateAfterRefund(sellerOrder);
        var statusChanged = !string.Equals(previousStatus, OrderStatusFlow.NormalizeStatus(sellerOrder.Status), StringComparison.OrdinalIgnoreCase);
        var trackingChanged = HasTrackingChanged(previousTrackingNumber, previousTrackingCarrier, previousTrackingUrl, sellerOrder);
        if (statusChanged || trackingChanged)
        {
            RecordShippingHistory(
                sellerOrder,
                sellerOrder.Status,
                sellerId,
                "seller",
                trackingNumber: sellerOrder.TrackingNumber,
                trackingCarrier: sellerOrder.TrackingCarrier,
                trackingUrl: sellerOrder.TrackingUrl);
        }

        if (string.Equals(targetStatus, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            await _escrowService.ReleaseSellerOrderEscrowToBuyerAsync(
                sellerOrder.Id,
                "Sub-order cancelled");
        }
        else
        {
            await _escrowService.UpdateEscrowForSellerOrderAsync(sellerOrder);
        }

        await _cartRepository.SaveChangesAsync();
        return OrderStatusResult.SuccessResult(sellerOrder.Status, sellerOrder.Order?.Status);
    }

    public async Task<OrderStatusResult> UpdateItemStatusesAsync(
        int sellerOrderId,
        string sellerId,
        IReadOnlyCollection<int>? shippedItemIds,
        IReadOnlyCollection<int>? cancelledItemIds,
        string? trackingNumber = null,
        string? trackingCarrier = null,
        string? trackingUrl = null)
    {
        var sellerOrder = await _cartRepository.GetSellerOrderAsync(sellerOrderId, sellerId);
        if (sellerOrder is null)
        {
            return OrderStatusResult.NotFound("Sub-order not found.");
        }

        var previousStatus = OrderStatusFlow.NormalizeStatus(sellerOrder.Status);
        var previousTrackingNumber = sellerOrder.TrackingNumber;
        var previousTrackingCarrier = sellerOrder.TrackingCarrier;
        var previousTrackingUrl = sellerOrder.TrackingUrl;
        var normalizedStatus = OrderStatusFlow.NormalizeStatus(sellerOrder.Status);
        if (string.Equals(normalizedStatus, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedStatus, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedStatus, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            return OrderStatusResult.Failed("Cannot update items for a cancelled, delivered, or refunded sub-order.");
        }

        var shipSet = new HashSet<int>(shippedItemIds ?? Array.Empty<int>());
        var cancelSet = new HashSet<int>(cancelledItemIds ?? Array.Empty<int>());
        if (shipSet.Count == 0 && cancelSet.Count == 0)
        {
            return OrderStatusResult.Failed("Select at least one item.");
        }

        var validItemIds = new HashSet<int>(sellerOrder.Items.Select(i => i.Id));
        if (shipSet.Any(id => !validItemIds.Contains(id)) || cancelSet.Any(id => !validItemIds.Contains(id)))
        {
            return OrderStatusResult.Failed("Invalid items selected.");
        }

        foreach (var item in sellerOrder.Items)
        {
            if (shipSet.Contains(item.Id))
            {
                if (!TryUpdateItemStatus(item, OrderStatus.Shipped))
                {
                    return OrderStatusResult.InvalidTransition(item.Status, OrderStatus.Shipped);
                }
            }

            if (cancelSet.Contains(item.Id))
            {
                if (!TryUpdateItemStatus(item, OrderStatus.Cancelled))
                {
                    return OrderStatusResult.InvalidTransition(item.Status, OrderStatus.Cancelled);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            sellerOrder.TrackingNumber = trackingNumber.Trim();
        }
        if (!string.IsNullOrWhiteSpace(trackingCarrier))
        {
            sellerOrder.TrackingCarrier = trackingCarrier.Trim();
        }
        if (!string.IsNullOrWhiteSpace(trackingUrl))
        {
            sellerOrder.TrackingUrl = trackingUrl.Trim();
        }

        RecalculateSellerOrderFromItems(sellerOrder);
        RollupOrderStatus(sellerOrder.Order);
        _commissionService.RecalculateAfterRefund(sellerOrder);
        var statusChanged = !string.Equals(previousStatus, OrderStatusFlow.NormalizeStatus(sellerOrder.Status), StringComparison.OrdinalIgnoreCase);
        var trackingChanged = HasTrackingChanged(previousTrackingNumber, previousTrackingCarrier, previousTrackingUrl, sellerOrder);
        if (statusChanged || trackingChanged)
        {
            RecordShippingHistory(
                sellerOrder,
                sellerOrder.Status,
                sellerId,
                "seller",
                trackingNumber: sellerOrder.TrackingNumber,
                trackingCarrier: sellerOrder.TrackingCarrier,
                trackingUrl: sellerOrder.TrackingUrl);
        }

        if (string.Equals(sellerOrder.Status, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            await _escrowService.ReleaseSellerOrderEscrowToBuyerAsync(
                sellerOrder.Id,
                "Sub-order cancelled");
        }
        else
        {
            await _escrowService.UpdateEscrowForSellerOrderAsync(sellerOrder);
        }

        await _cartRepository.SaveChangesAsync();
        return OrderStatusResult.SuccessResult(sellerOrder.Status, sellerOrder.Order?.Status);
    }

    public async Task<OrderStatusResult> CancelOrderAsync(int orderId, string buyerId)
    {
        var order = await _cartRepository.GetOrderAsync(orderId, buyerId);
        if (order is null)
        {
            return OrderStatusResult.NotFound("Order not found.");
        }

        if (order.SubOrders.Any(s => OrderStatusFlow.IsShippedOrBeyond(s.Status)))
        {
            return OrderStatusResult.Failed("Order cannot be cancelled after shipment.");
        }

        foreach (var sub in order.SubOrders)
        {
            if (!OrderStatusFlow.IsValidTransition(sub.Status, OrderStatus.Cancelled))
            {
                return OrderStatusResult.Failed("Order cannot be cancelled after shipment.");
            }

            sub.TrackingNumber = null;
            sub.TrackingCarrier = null;
            sub.TrackingUrl = null;
            foreach (var item in sub.Items)
            {
                TryUpdateItemStatus(item, OrderStatus.Cancelled);
            }

            RecalculateSellerOrderFromItems(sub);
            _commissionService.RecalculateAfterRefund(sub);
            RecordShippingHistory(
                sub,
                sub.Status,
                buyerId,
                "buyer",
                trackingNumber: sub.TrackingNumber,
                trackingCarrier: sub.TrackingCarrier,
                trackingUrl: sub.TrackingUrl,
                notes: "Order cancelled by buyer");
        }

        RollupOrderStatus(order);

        await _escrowService.ReleaseEscrowToBuyerAsync(order, "Order cancelled");
        await _cartRepository.SaveChangesAsync();
        return OrderStatusResult.SuccessResult(order.Status, order.Status);
    }

    public async Task<OrderStatusResult> MarkSubOrderDeliveredAsync(int orderId, int sellerOrderId, string buyerId)
    {
        var order = await _cartRepository.GetOrderAsync(orderId, buyerId);
        if (order is null)
        {
            return OrderStatusResult.NotFound("Order not found.");
        }

        var subOrder = order.SubOrders.FirstOrDefault(s => s.Id == sellerOrderId);
        if (subOrder is null)
        {
            return OrderStatusResult.NotFound("Sub-order not found.");
        }

        var targetStatus = OrderStatus.Delivered;
        if (!OrderStatusFlow.IsValidTransition(subOrder.Status, targetStatus))
        {
            return OrderStatusResult.InvalidTransition(subOrder.Status, targetStatus);
        }

        subOrder.Status = targetStatus;
        subOrder.DeliveredAt ??= DateTimeOffset.UtcNow;

        foreach (var item in subOrder.Items.Where(i =>
                     string.Equals(OrderStatusFlow.NormalizeStatus(i.Status), OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase)))
        {
            TryUpdateItemStatus(item, OrderStatus.Delivered);
        }

        RecalculateSellerOrderFromItems(subOrder);
        RollupOrderStatus(order);
        RecordShippingHistory(
            subOrder,
            subOrder.Status,
            buyerId,
            "buyer",
            trackingNumber: subOrder.TrackingNumber,
            trackingCarrier: subOrder.TrackingCarrier,
            trackingUrl: subOrder.TrackingUrl);

        await _cartRepository.SaveChangesAsync();
        return OrderStatusResult.SuccessResult(subOrder.Status, order.Status);
    }

    public async Task<OrderStatusResult> RefundSellerOrderAsync(
        int sellerOrderId,
        string sellerId,
        decimal refundAmount,
        string? reason = null)
    {
        var sellerOrder = await _cartRepository.GetSellerOrderAsync(sellerOrderId, sellerId);
        if (sellerOrder is null)
        {
            return OrderStatusResult.NotFound("Sub-order not found.");
        }

        if (refundAmount <= 0)
        {
            return OrderStatusResult.Failed("Refund amount must be greater than zero.");
        }

        return await RefundSellerOrderInternalAsync(
            sellerOrder,
            refundAmount,
            reason,
            overrideReturnRules: false);
    }

    public async Task<OrderStatusResult> RefundOrderAsync(
        int orderId,
        decimal? refundAmount = null,
        string? reason = null,
        bool overrideReturnRules = false)
    {
        var order = await _cartRepository.GetOrderWithSubOrdersAsync(orderId);
        if (order is null)
        {
            return OrderStatusResult.NotFound("Order not found.");
        }

        if (order.SubOrders.Count == 0)
        {
            return OrderStatusResult.Failed("Order has no items to refund.");
        }

        var outstandingTotal = order.SubOrders.Sum(s => Math.Max(0m, s.TotalAmount - s.RefundedAmount));
        if (outstandingTotal <= 0)
        {
            return OrderStatusResult.Failed("No refundable balance remains.");
        }

        var targetAmount = refundAmount.HasValue
            ? Math.Min(refundAmount.Value, outstandingTotal)
            : outstandingTotal;

        if (targetAmount <= 0)
        {
            return OrderStatusResult.Failed("Invalid refund amount.");
        }

        var allocations = AllocateRefunds(order.SubOrders, targetAmount);
        foreach (var allocation in allocations)
        {
            var result = await RefundSellerOrderInternalAsync(
                allocation.SellerOrder,
                allocation.Amount,
                reason,
                overrideReturnRules);

            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return OrderStatusResult.SuccessResult(order.Status, order.Status);
    }

    private static void ApplyStatusChange(
        SellerOrderModel sellerOrder,
        string targetStatus,
        string? trackingNumber,
        string? trackingCarrier,
        string? trackingUrl,
        decimal? refundAmount)
    {
        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            sellerOrder.TrackingNumber = trackingNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(trackingCarrier))
        {
            sellerOrder.TrackingCarrier = trackingCarrier.Trim();
        }

        if (!string.IsNullOrWhiteSpace(trackingUrl))
        {
            sellerOrder.TrackingUrl = trackingUrl.Trim();
        }

        if (string.Equals(targetStatus, OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in sellerOrder.Items)
            {
                TryUpdateItemStatus(item, OrderStatus.Shipped);
            }
        }

        if (string.Equals(targetStatus, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase))
        {
            sellerOrder.RefundedAmount = refundAmount ?? CalculateRefundedAmountFromItems(sellerOrder);
        }

        if (string.Equals(targetStatus, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            sellerOrder.TrackingNumber = null;
            sellerOrder.TrackingCarrier = null;
            sellerOrder.TrackingUrl = null;
            foreach (var item in sellerOrder.Items)
            {
                TryUpdateItemStatus(item, OrderStatus.Cancelled);
            }
        }

        if (string.Equals(targetStatus, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            sellerOrder.DeliveredAt ??= DateTimeOffset.UtcNow;
            foreach (var item in sellerOrder.Items.Where(i =>
                         string.Equals(OrderStatusFlow.NormalizeStatus(i.Status), OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase)))
            {
                TryUpdateItemStatus(item, OrderStatus.Delivered);
            }
        }

        sellerOrder.Status = targetStatus;
    }

    private static void RollupOrderStatus(OrderModel? order)
    {
        if (order is null)
        {
            return;
        }

        order.Status = OrderStatusFlow.CalculateOverallStatus(order);
        order.RefundedAmount = OrderStatusFlow.CalculateRefundedAmount(order);
    }

    private static void RecalculateSellerOrderFromItems(SellerOrderModel sellerOrder, decimal? manualRefund = null)
    {
        if (sellerOrder.Items.Count == 0)
        {
            sellerOrder.RefundedAmount = manualRefund ?? sellerOrder.RefundedAmount;
            return;
        }

        var normalizedStatuses = sellerOrder.Items
            .Select(i => OrderStatusFlow.NormalizeStatus(i.Status))
            .ToList();

        var allCancelled = normalizedStatuses.All(s =>
            string.Equals(s, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase));
        var allDelivered = normalizedStatuses.All(s =>
            string.Equals(s, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase));
        var allShippedOrBeyond = normalizedStatuses.All(s =>
            string.Equals(s, OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase));
        var anyShippedOrDelivered = normalizedStatuses.Any(s =>
            string.Equals(s, OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase));

        if (allCancelled)
        {
            sellerOrder.Status = OrderStatus.Cancelled;
            sellerOrder.TrackingNumber = null;
            sellerOrder.TrackingCarrier = null;
            sellerOrder.TrackingUrl = null;
            sellerOrder.DeliveredAt = null;
        }
        else if (allDelivered)
        {
            sellerOrder.Status = OrderStatus.Delivered;
            sellerOrder.DeliveredAt ??= DateTimeOffset.UtcNow;
        }
        else if (allShippedOrBeyond && anyShippedOrDelivered)
        {
            sellerOrder.Status = OrderStatus.Shipped;
        }
        else if (anyShippedOrDelivered)
        {
            sellerOrder.Status = OrderStatus.Preparing;
        }
        else
        {
            sellerOrder.Status = OrderStatusFlow.NormalizeStatus(sellerOrder.Status);
        }

        sellerOrder.RefundedAmount = manualRefund ?? CalculateRefundedAmountFromItems(sellerOrder);
    }

    private static decimal CalculateRefundedAmountFromItems(SellerOrderModel sellerOrder)
    {
        if (sellerOrder.Items.Count == 0)
        {
            return sellerOrder.Status.Equals(OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase)
                ? sellerOrder.TotalAmount
                : 0m;
        }

        var subtotal = sellerOrder.ItemsSubtotal;
        var cancelledTotal = 0m;

        foreach (var item in sellerOrder.Items)
        {
            if (!string.Equals(item.Status, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lineTotal = item.UnitPrice * item.Quantity;
            var discountShare = subtotal > 0 && sellerOrder.DiscountTotal > 0
                ? Math.Round(sellerOrder.DiscountTotal * (lineTotal / subtotal), 2, MidpointRounding.AwayFromZero)
                : 0m;

            cancelledTotal += Math.Max(0m, lineTotal - discountShare);
        }

        if (sellerOrder.Items.All(i => string.Equals(i.Status, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase)))
        {
            cancelledTotal += sellerOrder.ShippingTotal;
        }

        var maxRefund = Math.Max(0m, sellerOrder.TotalAmount);
        return Math.Min(cancelledTotal, maxRefund);
    }

    private static bool TryUpdateItemStatus(OrderItemModel item, string targetStatus)
    {
        if (!OrderStatusFlow.IsValidTransition(item.Status, targetStatus))
        {
            return false;
        }

        item.Status = targetStatus;
        return true;
    }

    private static bool HasTrackingChanged(
        string? previousNumber,
        string? previousCarrier,
        string? previousUrl,
        SellerOrderModel sellerOrder)
    {
        return !string.Equals(previousNumber ?? string.Empty, sellerOrder.TrackingNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(previousCarrier ?? string.Empty, sellerOrder.TrackingCarrier ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(previousUrl ?? string.Empty, sellerOrder.TrackingUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private void RecordShippingHistory(
        SellerOrderModel sellerOrder,
        string status,
        string? changedBy,
        string actorRole,
        string? notes = null,
        string? trackingNumber = null,
        string? trackingCarrier = null,
        string? trackingUrl = null)
    {
        var normalizedStatus = OrderStatusFlow.NormalizeStatus(status);
        var last = sellerOrder.ShippingHistory
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefault();

        if (last is not null &&
            string.Equals(last.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(trackingNumber) &&
            string.IsNullOrWhiteSpace(trackingCarrier) &&
            string.IsNullOrWhiteSpace(trackingUrl))
        {
            return;
        }

        sellerOrder.ShippingHistory.Add(new ShippingStatusHistory
        {
            SellerOrderId = sellerOrder.Id,
            Status = normalizedStatus,
            ChangedBy = changedBy,
            ChangedByRole = actorRole,
            Notes = notes,
            TrackingNumber = trackingNumber,
            TrackingCarrier = trackingCarrier,
            TrackingUrl = trackingUrl,
            ChangedAt = _timeProvider.GetUtcNow()
        });
    }

    private async Task<OrderStatusResult> RefundSellerOrderInternalAsync(
        SellerOrderModel sellerOrder,
        decimal refundAmount,
        string? reason,
        bool overrideReturnRules)
    {
        var now = _timeProvider.GetUtcNow();
        if (!overrideReturnRules)
        {
            if (!sellerOrder.DeliveredAt.HasValue)
            {
                return OrderStatusResult.Failed("Order must be delivered before processing a refund.");
            }

            var deadline = sellerOrder.DeliveredAt.Value.AddDays(14);
            if (deadline < now)
            {
                return OrderStatusResult.Failed("Refund window has expired.");
            }
        }

        var outstanding = Math.Max(0m, sellerOrder.TotalAmount - sellerOrder.RefundedAmount);
        if (outstanding <= 0)
        {
            return OrderStatusResult.Failed("No refundable balance remains.");
        }

        var applied = Math.Min(refundAmount, outstanding);
        if (applied <= 0)
        {
            return OrderStatusResult.Failed("Invalid refund amount.");
        }

        sellerOrder.RefundedAmount += applied;
        var fullyRefunded = sellerOrder.RefundedAmount >= sellerOrder.TotalAmount - 0.01m;
        if (fullyRefunded)
        {
            sellerOrder.Status = OrderStatus.Refunded;
            sellerOrder.TrackingNumber = null;
            sellerOrder.TrackingCarrier = null;
            sellerOrder.TrackingUrl = null;
            RecordShippingHistory(
                sellerOrder,
                sellerOrder.Status,
                sellerOrder.SellerId,
                "seller",
                notes: string.IsNullOrWhiteSpace(reason) ? "Refund processed" : reason);
        }

        _commissionService.RecalculateAfterRefund(sellerOrder);

        if (fullyRefunded)
        {
            await _escrowService.ReleaseSellerOrderEscrowToBuyerAsync(
                sellerOrder.Id,
                string.IsNullOrWhiteSpace(reason) ? "Refund processed" : reason);
        }
        else
        {
            await _escrowService.UpdateEscrowForSellerOrderAsync(sellerOrder);
        }

        RollupOrderStatus(sellerOrder.Order);
        await _cartRepository.SaveChangesAsync();
        return OrderStatusResult.SuccessResult(sellerOrder.Status, sellerOrder.Order?.Status);
    }

    private static List<RefundAllocation> AllocateRefunds(
        IEnumerable<SellerOrderModel> sellerOrders,
        decimal targetAmount)
    {
        var orders = sellerOrders.ToList();
        var outstandingTotal = orders.Sum(o => Math.Max(0m, o.TotalAmount - o.RefundedAmount));
        var allocations = new List<RefundAllocation>();

        if (outstandingTotal <= 0 || targetAmount <= 0)
        {
            return allocations;
        }

        var remaining = targetAmount;
        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            var outstanding = Math.Max(0m, order.TotalAmount - order.RefundedAmount);
            if (outstanding <= 0)
            {
                continue;
            }

            decimal allocation;
            if (i == orders.Count - 1)
            {
                allocation = Math.Min(outstanding, remaining);
            }
            else
            {
                var share = outstanding / outstandingTotal;
                allocation = Math.Min(outstanding, Math.Round(targetAmount * share, 2, MidpointRounding.AwayFromZero));
            }

            remaining -= allocation;
            allocations.Add(new RefundAllocation(order, allocation));
        }

        if (remaining > 0 && allocations.Count > 0)
        {
            var last = allocations[^1];
            allocations[^1] = last with { Amount = last.Amount + remaining };
        }

        return allocations.Where(a => a.Amount > 0).ToList();
    }

    private record RefundAllocation(SellerOrderModel SellerOrder, decimal Amount);
}

public record OrderStatusResult(bool IsSuccess, string? Error, string? SubOrderStatus = null, string? OrderStatus = null)
{
    public static OrderStatusResult Failed(string message) => new(false, message);
    public static OrderStatusResult NotFound(string message) => new(false, message);
    public static OrderStatusResult InvalidTransition(string current, string target) =>
        new(false, $"Cannot move from {current} to {target}.");
    public static OrderStatusResult SuccessResult(string? subOrderStatus, string? orderStatus) =>
        new(true, null, subOrderStatus, orderStatus);
}
