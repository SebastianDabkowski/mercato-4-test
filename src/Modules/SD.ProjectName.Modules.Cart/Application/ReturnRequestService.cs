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
    private static readonly HashSet<string> OpenStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ReturnRequestStatus.Requested,
        ReturnRequestStatus.Approved,
        ReturnRequestStatus.InfoRequested,
        ReturnRequestStatus.PartialProposed
    };
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ReturnRequestType.Return,
        ReturnRequestType.Complaint
    };

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
            return ReturnEligibility.NotAllowed("Return or complaint requests are available after delivery.");
        }

        var deliveredAt = subOrder.DeliveredAt ?? order.CreatedAt;
        var deadline = deliveredAt.Add(ReturnWindow);
        var now = _timeProvider.GetUtcNow();
        if (now > deadline)
        {
            return ReturnEligibility.NotAllowed("Request window has expired.");
        }

        var availableItems = subOrder.Items
            .Where(i => !string.Equals(OrderStatusFlow.NormalizeStatus(i.Status), OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var openItemIds = GetOpenItemIds(subOrder);
        var hasAvailableItems = availableItems.Any(i => !openItemIds.Contains(i.Id));
        if (!hasAvailableItems)
        {
            return ReturnEligibility.NotAllowed("An open case already exists for these items.");
        }

        return ReturnEligibility.Allowed(deadline);
    }

    public async Task<ReturnRequestResult> CreateAsync(
        int orderId,
        int sellerOrderId,
        string buyerId,
        IReadOnlyCollection<int> itemIds,
        string requestType,
        string reason,
        string description)
    {
        var normalizedType = NormalizeRequestType(requestType);
        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return ReturnRequestResult.Failed("Please choose request type.");
        }

        var trimmedReason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            return ReturnRequestResult.Failed("Please provide a reason.");
        }

        var trimmedDescription = description?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedDescription))
        {
            return ReturnRequestResult.Failed("Please provide a description.");
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

        var availableItems = subOrder.Items
            .Where(i => !string.Equals(OrderStatusFlow.NormalizeStatus(i.Status), OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var openItemIds = GetOpenItemIds(subOrder);
        if (!availableItems.Any(i => !openItemIds.Contains(i.Id)))
        {
            return ReturnRequestResult.Failed("An open case already exists for the selected items.");
        }

        var requestedIds = itemIds?.Where(id => id > 0).ToList() ?? new List<int>();
        if (requestedIds.Any(id => openItemIds.Contains(id)))
        {
            return ReturnRequestResult.Failed("An open case already exists for the selected items.");
        }

        var resolvedItems = ResolveItems(availableItems, requestedIds, openItemIds);
        if (resolvedItems is null || resolvedItems.Count == 0)
        {
            return ReturnRequestResult.Failed("Selected items are not valid for this sub-order.");
        }

        var request = new ReturnRequestModel
        {
            OrderId = order.Id,
            SellerOrderId = subOrder.Id,
            BuyerId = order.BuyerId,
            RequestType = normalizedType,
            Status = ReturnRequestStatus.Requested,
            Reason = trimmedReason,
            Description = trimmedDescription,
            RequestedAt = _timeProvider.GetUtcNow(),
            Items = resolvedItems
        };

        var saved = await _cartRepository.AddReturnRequestAsync(request);
        return ReturnRequestResult.Success(saved);
    }

    public HashSet<int> GetOpenItemIds(SellerOrderModel subOrder)
    {
        var requests = subOrder.ReturnRequests ?? new List<ReturnRequestModel>();
        return requests
            .Where(r => OpenStatuses.Contains(r.Status))
            .SelectMany(r => r.Items ?? new List<ReturnRequestItemModel>())
            .Select(i => i.OrderItemId)
            .ToHashSet();
    }

    private static List<ReturnRequestItemModel>? ResolveItems(
        List<OrderItemModel> availableItems,
        List<int> itemIds,
        HashSet<int> openItemIds)
    {
        var ids = itemIds ?? new List<int>();
        var selectableItems = availableItems.Where(i => !openItemIds.Contains(i.Id)).ToList();
        if (!selectableItems.Any())
        {
            return null;
        }

        var availableIds = new HashSet<int>(selectableItems.Select(i => i.Id));

        if (ids.Count == 0)
        {
            return selectableItems
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

        return selectableItems
            .Where(i => ids.Contains(i.Id))
            .Select(i => new ReturnRequestItemModel
            {
                OrderItemId = i.Id,
                Quantity = i.Quantity
            })
            .ToList();
    }

    private static string NormalizeRequestType(string requestType)
    {
        if (string.IsNullOrWhiteSpace(requestType))
        {
            return string.Empty;
        }

        var trimmed = requestType.Trim();
        return AllowedTypes.FirstOrDefault(t => string.Equals(t, trimmed, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
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
