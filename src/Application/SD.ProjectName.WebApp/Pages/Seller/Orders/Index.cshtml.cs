using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller.Orders;

[Authorize(Roles = IdentityRoles.Seller)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICartRepository _cartRepository;

    public IndexModel(UserManager<ApplicationUser> userManager, ICartRepository cartRepository)
    {
        _userManager = userManager;
        _cartRepository = cartRepository;
    }

    public List<SellerOrderModel> Orders { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        Orders = await _cartRepository.GetSellerOrdersAsync(user.Id);
        Orders = Orders.OrderByDescending(o => o.Order?.CreatedAt ?? o.Id).ToList();

        return Page();
    }
}
