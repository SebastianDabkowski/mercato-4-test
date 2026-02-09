using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
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
    private readonly OrderStatusService _orderStatusService;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        ICartRepository cartRepository,
        OrderStatusService orderStatusService)
    {
        _userManager = userManager;
        _cartRepository = cartRepository;
        _orderStatusService = orderStatusService;
    }

    public List<SellerOrderModel> Orders { get; private set; } = new();
    [TempData]
    public string? StatusMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        await LoadOrders(user.Id);

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int sellerOrderId, string status, string? trackingNumber)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await _orderStatusService.UpdateSellerOrderStatusAsync(sellerOrderId, user.Id, status, trackingNumber);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
        }
        else
        {
            StatusMessage = "Status updated.";
        }

        await LoadOrders(user.Id);
        return Page();
    }

    private async Task LoadOrders(string sellerId)
    {
        Orders = await _cartRepository.GetSellerOrdersAsync(sellerId);
        Orders = Orders.OrderByDescending(o => o.Order?.CreatedAt ?? DateTimeOffset.MinValue).ToList();
    }
}
