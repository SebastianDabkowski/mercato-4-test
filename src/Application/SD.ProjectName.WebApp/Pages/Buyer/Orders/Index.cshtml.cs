using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer.Orders;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ICartIdentityService _cartIdentityService;
    private readonly ICartRepository _cartRepository;

    public IndexModel(ICartIdentityService cartIdentityService, ICartRepository cartRepository)
    {
        _cartIdentityService = cartIdentityService;
        _cartRepository = cartRepository;
    }

    public List<OrderModel> Orders { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        Orders = await _cartRepository.GetOrdersForBuyerAsync(buyerId);
        return Page();
    }
}
