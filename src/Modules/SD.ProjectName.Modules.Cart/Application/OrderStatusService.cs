using System;
using System.Linq;
using System.Threading.Tasks;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class OrderStatusService
{
    private readonly ICartRepository _cartRepository;

    public OrderStatusService(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
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
        RollupOrderStatus(sellerOrder.Order);

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

            sub.Status = OrderStatus.Cancelled;
            sub.RefundedAmount = 0m;
            sub.TrackingNumber = null;
        }

        order.Status = OrderStatus.Cancelled;
        order.RefundedAmount = 0m;

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
        }

        if (string.Equals(targetStatus, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase))
        {
            sellerOrder.RefundedAmount = refundAmount ?? sellerOrder.TotalAmount;
        }

        if (string.Equals(targetStatus, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            sellerOrder.TrackingNumber = null;
        }

        if (string.Equals(targetStatus, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            sellerOrder.DeliveredAt ??= DateTimeOffset.UtcNow;
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
