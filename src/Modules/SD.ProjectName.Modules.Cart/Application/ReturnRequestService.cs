using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class ReturnRequestService
{
    private readonly ICartRepository _cartRepository;
    private readonly TimeProvider _timeProvider;
    private static readonly TimeSpan ReturnWindow = TimeSpan.FromDays(14);

    public ReturnRequestService(ICartRepository cartRepository, TimeProvider timeProvider)
    {
        _cartRepository = cartRepository;
        _timeProvider = timeProvider;
    }

    public ReturnEligibility EvaluateEligibility(OrderModel order, SellerOrderModel subOrder)
    {
        var normalizedStatus = OrderStatusFlow.NormalizeStatus(subOrder.Status);
        if (!string.Equals(normalizedStatus, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            return ReturnEligibility.NotAllowed("Return requests are available after delivery.");
        }

        var deliveredAt = subOrder.DeliveredAt ?? order.CreatedAt;
        var deadline = deliveredAt.Add(ReturnWindow);
        var now = _timeProvider.GetUtcNow();
        if (now > deadline)
        {
            return ReturnEligibility.NotAllowed("Return window has expired.");
        }

        var hasPending = subOrder.ReturnRequests.Any(r =>
            string.Equals(r.Status, ReturnRequestStatus.Requested, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r.Status, ReturnRequestStatus.Approved, StringComparison.OrdinalIgnoreCase));
        if (hasPending)
        {
            return ReturnEligibility.NotAllowed("A return request already exists for this sub-order.");
        }

        return ReturnEligibility.Allowed(deadline);
    }

    public async Task<ReturnRequestResult> CreateAsync(
        int orderId,
        int sellerOrderId,
        string buyerId,
        IReadOnlyCollection<int> itemIds,
        string reason)
    {
        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            return ReturnRequestResult.Failed("Please provide a return reason.");
        }

        var order = await _cartRepository.GetOrderAsync(orderId, buyerId);
        if (order is null)
        {
            return ReturnRequestResult.NotFound("Order not found.");
        }

        var subOrder = order.SubOrders.FirstOrDefault(s => s.Id == sellerOrderId);
        if (subOrder is null)
        {
            return ReturnRequestResult.NotFound("Sub-order not found.");
        }

        var eligibility = EvaluateEligibility(order, subOrder);
        if (!eligibility.IsAllowed)
        {
            return ReturnRequestResult.Failed(eligibility.Message ?? "Return request is not allowed.");
        }

        var resolvedItems = ResolveItems(subOrder, itemIds);
        if (resolvedItems is null || resolvedItems.Count == 0)
        {
            return ReturnRequestResult.Failed("Selected items are not valid for this sub-order.");
        }

        var request = new ReturnRequestModel
        {
            OrderId = order.Id,
            SellerOrderId = subOrder.Id,
            BuyerId = order.BuyerId,
            Status = ReturnRequestStatus.Requested,
            Reason = trimmedReason,
            RequestedAt = _timeProvider.GetUtcNow(),
            Items = resolvedItems
        };

        var saved = await _cartRepository.AddReturnRequestAsync(request);
        return ReturnRequestResult.Success(saved);
    }

    private static List<ReturnRequestItemModel>? ResolveItems(SellerOrderModel subOrder, IReadOnlyCollection<int> itemIds)
    {
        var ids = itemIds?.Where(id => id > 0).ToList() ?? new List<int>();
        var availableIds = new HashSet<int>(subOrder.Items.Select(i => i.Id));

        if (ids.Count == 0)
        {
            return subOrder.Items
                .Select(i => new ReturnRequestItemModel
                {
                    OrderItemId = i.Id,
                    Quantity = i.Quantity
                })
                .ToList();
        }

        if (ids.Any(id => !availableIds.Contains(id)))
        {
            return null;
        }

        return subOrder.Items
            .Where(i => ids.Contains(i.Id))
            .Select(i => new ReturnRequestItemModel
            {
                OrderItemId = i.Id,
                Quantity = i.Quantity
            })
            .ToList();
    }

    public TimeSpan ReturnWindowPeriod => ReturnWindow;
}

public record ReturnEligibility(bool IsAllowed, string? Message, DateTimeOffset? Deadline)
{
    public static ReturnEligibility Allowed(DateTimeOffset? deadline) => new(true, null, deadline);
    public static ReturnEligibility NotAllowed(string message) => new(false, message, null);
}

public record ReturnRequestResult(bool IsSuccess, string? Error, ReturnRequestModel? Request)
{
    public static ReturnRequestResult Success(ReturnRequestModel request) => new(true, null, request);
    public static ReturnRequestResult Failed(string message) => new(false, message, null);
    public static ReturnRequestResult NotFound(string message) => new(false, message, null);
}
