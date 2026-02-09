using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Admin.Orders;

[Authorize(Roles = IdentityRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly ICartRepository _cartRepository;

    public DetailsModel(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public OrderModel? Order { get; private set; }

    public async Task<IActionResult> OnGetAsync(int orderId)
    {
        Order = await _cartRepository.GetOrderWithSubOrdersAsync(orderId);
        if (Order is null)
        {
            return NotFound();
        }

        return Page();
    }
}
