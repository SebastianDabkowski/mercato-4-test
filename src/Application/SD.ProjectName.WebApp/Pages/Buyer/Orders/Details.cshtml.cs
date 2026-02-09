using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer.Orders;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly ICartRepository _cartRepository;
    private readonly OrderStatusService _orderStatusService;
    private readonly ReturnRequestService _returnRequestService;

    public DetailsModel(
        ICartIdentityService cartIdentityService,
        ICartRepository cartRepository,
        OrderStatusService orderStatusService,
        ReturnRequestService returnRequestService)
    {
        _cartIdentityService = cartIdentityService;
        _cartRepository = cartRepository;
        _orderStatusService = orderStatusService;
        _returnRequestService = returnRequestService;
    }

    public OrderModel? Order { get; private set; }
    public string EstimatedDeliveryText { get; private set; } = string.Empty;
    public string OverallStatus { get; private set; } = string.Empty;
    public string PaymentStatusText { get; private set; } = string.Empty;
    public bool IsPaymentFailed { get; private set; }
    public bool CanCancel { get; private set; }
    public Dictionary<int, ReturnRequestInfo> ReturnInfo { get; } = new();
    public string PendingStatusLabel => GetCaseStatusLabel(ReturnRequestStatus.Requested);

    public async Task<IActionResult> OnGetAsync(int orderId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        Order = await _cartRepository.GetOrderAsync(orderId, buyerId);
        if (Order is null)
        {
            var existingOrder = await _cartRepository.GetOrderWithSubOrdersAsync(orderId);
            if (existingOrder is not null)
            {
                return Forbid();
            }

            return NotFound();
        }

        OverallStatus = OrderStatusFlow.NormalizeStatus(OrderStatusFlow.CalculateOverallStatus(Order));
        var paymentSelection = await _cartRepository.GetPaymentSelectionByOrderIdAsync(orderId);
        PaymentStatusText = ResolvePaymentStatus(Order, paymentSelection);
        IsPaymentFailed = string.Equals(PaymentStatusText, "Failed", StringComparison.OrdinalIgnoreCase);
        CanCancel = Order.SubOrders.All(o => OrderStatusFlow.CanCancel(o.Status)) &&
                    !string.Equals(OverallStatus, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(OverallStatus, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(OverallStatus, OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(OverallStatus, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase);
        EstimatedDeliveryText = ResolveEstimatedDelivery(Order);
        foreach (var subOrder in Order.SubOrders)
        {
            var eligibility = _returnRequestService.EvaluateEligibility(Order, subOrder);
            var latest = subOrder.ReturnRequests
                .OrderByDescending(r => r.RequestedAt)
                .FirstOrDefault();
            var openItemIds = _returnRequestService.GetOpenItemIds(subOrder);
            ReturnInfo[subOrder.Id] = new ReturnRequestInfo(eligibility, latest, openItemIds);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int orderId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _orderStatusService.CancelOrderAsync(orderId, buyerId);
        if (!result.IsSuccess)
        {
            TempData["OrderError"] = result.Error;
        }
        else
        {
            TempData["OrderSuccess"] = "Order cancelled.";
        }

        return RedirectToPage(new { orderId });
    }

    private static string ResolvePaymentStatus(OrderModel order, PaymentSelectionModel? paymentSelection)
    {
        if (paymentSelection is not null)
        {
            var normalized = NormalizePaymentStatus(paymentSelection.Status);
            return normalized switch
            {
                PaymentStatus.Paid => "Paid",
                PaymentStatus.Pending => "Pending",
                PaymentStatus.Failed => "Failed",
                PaymentStatus.Refunded => "Refunded",
                _ => "Pending"
            };
        }

        var normalizedOrderStatus = OrderStatusFlow.NormalizeStatus(order.Status);
        return normalizedOrderStatus switch
        {
            OrderStatus.Refunded => "Refunded",
            OrderStatus.Failed => "Failed",
            OrderStatus.Paid => "Paid",
            OrderStatus.New => "Pending",
            OrderStatus.Pending => "Pending",
            _ => "Paid"
        };
    }

    private static PaymentStatus NormalizePaymentStatus(PaymentStatus status) =>
        status switch
        {
            PaymentStatus.Authorized => PaymentStatus.Paid,
            PaymentStatus.Cancelled => PaymentStatus.Failed,
            _ => status
        };

    public async Task<IActionResult> OnPostMarkDeliveredAsync(int orderId, int subOrderId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _orderStatusService.MarkSubOrderDeliveredAsync(orderId, subOrderId, buyerId);
        if (!result.IsSuccess)
        {
            TempData["OrderError"] = result.Error;
        }
        else
        {
            TempData["OrderSuccess"] = "Delivery confirmed.";
        }

        return RedirectToPage(new { orderId });
    }

    public async Task<IActionResult> OnPostRequestReturnAsync(
        int orderId,
        int subOrderId,
        List<int>? itemIds,
        string requestType,
        string reason,
        string description)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _returnRequestService.CreateAsync(
            orderId,
            subOrderId,
            buyerId,
            itemIds ?? new List<int>(),
            requestType ?? string.Empty,
            reason ?? string.Empty,
            description ?? string.Empty);

        if (!result.IsSuccess)
        {
            TempData["OrderError"] = result.Error;
        }
        else
        {
            var typeLabel = GetCaseTypeLabel(result.Request!.RequestType);
            TempData["OrderSuccess"] = $"{typeLabel} request submitted. Case ID: #{result.Request!.Id}. Status: {PendingStatusLabel}.";
        }

        return RedirectToPage(new { orderId });
    }

    private static string ResolveEstimatedDelivery(OrderModel order)
    {
        var deliveryEstimate = order.ShippingSelections
            .Select(s => s.DeliveryEstimate)
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
        if (!string.IsNullOrWhiteSpace(deliveryEstimate))
        {
            return deliveryEstimate!;
        }

        var estimated = order.ShippingSelections
            .Where(s => s.EstimatedDeliveryDate.HasValue)
            .OrderBy(s => s.EstimatedDeliveryDate)
            .FirstOrDefault();

        return estimated?.EstimatedDeliveryDate?.ToLocalTime().ToString("D") ?? "Not available";
    }

    public string GetCaseStatusLabel(string status) =>
        status?.ToLowerInvariant() switch
        {
            ReturnRequestStatus.Requested => "Pending seller review",
            ReturnRequestStatus.Approved => "Approved",
            ReturnRequestStatus.PartialProposed => "Partial solution proposed",
            ReturnRequestStatus.InfoRequested => "More information requested",
            ReturnRequestStatus.Rejected => "Rejected",
            ReturnRequestStatus.Completed => "Completed",
            _ => "Pending seller review"
        };

    public string GetCaseBadgeClass(string status) =>
        status?.ToLowerInvariant() switch
        {
            ReturnRequestStatus.Approved => "bg-success",
            ReturnRequestStatus.Rejected => "bg-danger",
            ReturnRequestStatus.Completed => "bg-secondary",
            ReturnRequestStatus.PartialProposed => "bg-info text-dark",
            _ => "bg-warning text-dark"
        };

    public string GetCaseTypeLabel(string requestType) =>
        string.Equals(requestType, ReturnRequestType.Complaint, StringComparison.OrdinalIgnoreCase)
            ? "Complaint"
            : "Return";

    public record ReturnRequestInfo(ReturnEligibility Eligibility, ReturnRequestModel? LatestRequest, HashSet<int> OpenItemIds);
}
