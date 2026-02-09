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
    public bool CanCancel { get; private set; }
    public Dictionary<int, ReturnRequestInfo> ReturnInfo { get; } = new();

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
            ReturnInfo[subOrder.Id] = new ReturnRequestInfo(eligibility, latest);
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

    public async Task<IActionResult> OnPostRequestReturnAsync(int orderId, int subOrderId, List<int>? itemIds, string reason)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await _returnRequestService.CreateAsync(
            orderId,
            subOrderId,
            buyerId,
            itemIds ?? new List<int>(),
            reason ?? string.Empty);

        if (!result.IsSuccess)
        {
            TempData["OrderError"] = result.Error;
        }
        else
        {
            TempData["OrderSuccess"] = "Return request submitted.";
        }

        return RedirectToPage(new { orderId });
    }

    private static string ResolveEstimatedDelivery(OrderModel order)
    {
        var estimated = order.ShippingSelections
            .Where(s => s.EstimatedDeliveryDate.HasValue)
            .OrderBy(s => s.EstimatedDeliveryDate)
            .FirstOrDefault();

        return estimated?.EstimatedDeliveryDate?.ToLocalTime().ToString("D") ?? "Not available";
    }

    public record ReturnRequestInfo(ReturnEligibility Eligibility, ReturnRequestModel? LatestRequest);
}
