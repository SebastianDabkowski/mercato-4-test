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

    public DetailsModel(
        ICartIdentityService cartIdentityService,
        ICartRepository cartRepository,
        OrderStatusService orderStatusService)
    {
        _cartIdentityService = cartIdentityService;
        _cartRepository = cartRepository;
        _orderStatusService = orderStatusService;
    }

    public OrderModel? Order { get; private set; }
    public string EstimatedDeliveryText { get; private set; } = string.Empty;
    public string OverallStatus { get; private set; } = string.Empty;
    public bool CanCancel { get; private set; }

    public async Task<IActionResult> OnGetAsync(int orderId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        Order = await _cartRepository.GetOrderAsync(orderId, buyerId);
        if (Order is null)
        {
            return NotFound();
        }

        OverallStatus = OrderStatusFlow.CalculateOverallStatus(Order);
        CanCancel = Order.SubOrders.All(o => OrderStatusFlow.CanCancel(o.Status)) &&
                    !string.Equals(OverallStatus, OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(OverallStatus, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(OverallStatus, OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(OverallStatus, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase);
        EstimatedDeliveryText = ResolveEstimatedDelivery(Order);
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

    private static string ResolveEstimatedDelivery(OrderModel order)
    {
        var estimated = order.ShippingSelections
            .Where(s => s.EstimatedDeliveryDate.HasValue)
            .OrderBy(s => s.EstimatedDeliveryDate)
            .FirstOrDefault();

        return estimated?.EstimatedDeliveryDate?.ToLocalTime().ToString("D") ?? "Not available";
    }
}
