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

    public OrderStatusService(ICartRepository cartRepository, EscrowService escrowService)
    {
        _cartRepository = cartRepository;
        _escrowService = escrowService;
    }

    public async Task<OrderStatusResult> UpdateSellerOrderStatusAsync(
        int sellerOrderId,
        string sellerId,
        string targetStatus,
        string? trackingNumber = null,
        decimal? refundAmount = null)
    {
        var sellerOrder = await _cartRepository.GetSellerOrderAsync(sellerOrderId, sellerId);
        if (sellerOrder is null)
        {
            return OrderStatusResult.NotFound("Sub-order not found.");
        }

        if (!OrderStatusFlow.IsValidTransition(sellerOrder.Status, targetStatus))
        {
            return OrderStatusResult.InvalidTransition(sellerOrder.Status, targetStatus);
        }

        ApplyStatusChange(sellerOrder, targetStatus, trackingNumber, refundAmount);
        var refundOverride = string.Equals(targetStatus, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase)
            ? refundAmount
            : null;

        RecalculateSellerOrderFromItems(sellerOrder, refundOverride);
        RollupOrderStatus(sellerOrder.Order);

        if (string.Equals(targetStatus, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            await _escrowService.ReleaseSellerOrderEscrowToBuyerAsync(
                sellerOrder.Id,
                "Sub-order cancelled");
        }

        await _cartRepository.SaveChangesAsync();
        return OrderStatusResult.SuccessResult(sellerOrder.Status, sellerOrder.Order?.Status);
    }

    public async Task<OrderStatusResult> UpdateItemStatusesAsync(
        int sellerOrderId,
        string sellerId,
        IReadOnlyCollection<int>? shippedItemIds,
        IReadOnlyCollection<int>? cancelledItemIds,
        string? trackingNumber = null)
    {
        var sellerOrder = await _cartRepository.GetSellerOrderAsync(sellerOrderId, sellerId);
        if (sellerOrder is null)
        {
            return OrderStatusResult.NotFound("Sub-order not found.");
        }

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

        RecalculateSellerOrderFromItems(sellerOrder);
        RollupOrderStatus(sellerOrder.Order);

        if (string.Equals(sellerOrder.Status, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            await _escrowService.ReleaseSellerOrderEscrowToBuyerAsync(
                sellerOrder.Id,
                "Sub-order cancelled");
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
            foreach (var item in sub.Items)
            {
                TryUpdateItemStatus(item, OrderStatus.Cancelled);
            }

            RecalculateSellerOrderFromItems(sub);
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

        await _cartRepository.SaveChangesAsync();
        return OrderStatusResult.SuccessResult(subOrder.Status, order.Status);
    }

    private static void ApplyStatusChange(
        SellerOrderModel sellerOrder,
        string targetStatus,
        string? trackingNumber,
        decimal? refundAmount)
    {
        if (string.Equals(targetStatus, OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase))
        {
            sellerOrder.TrackingNumber = string.IsNullOrWhiteSpace(trackingNumber)
                ? sellerOrder.TrackingNumber
                : trackingNumber.Trim();

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
