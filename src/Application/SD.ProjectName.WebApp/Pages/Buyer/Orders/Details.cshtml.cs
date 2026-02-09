using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer.Orders;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly ICartRepository _cartRepository;

    public DetailsModel(ICartIdentityService cartIdentityService, ICartRepository cartRepository)
    {
        _cartIdentityService = cartIdentityService;
        _cartRepository = cartRepository;
    }

    public OrderModel? Order { get; private set; }
    public string EstimatedDeliveryText { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int orderId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        Order = await _cartRepository.GetOrderAsync(orderId, buyerId);
        if (Order is null)
        {
            return NotFound();
        }

        EstimatedDeliveryText = ResolveEstimatedDelivery(Order);
        return Page();
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
