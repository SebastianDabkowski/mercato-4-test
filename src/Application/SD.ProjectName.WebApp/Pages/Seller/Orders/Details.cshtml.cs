using System;
using System.Collections.Generic;
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
public class DetailsModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICartRepository _cartRepository;
    private readonly OrderStatusService _orderStatusService;

    public DetailsModel(
        UserManager<ApplicationUser> userManager,
        ICartRepository cartRepository,
        OrderStatusService orderStatusService)
    {
        _userManager = userManager;
        _cartRepository = cartRepository;
        _orderStatusService = orderStatusService;
    }

    public SellerOrderModel? SellerOrder { get; private set; }
    public OrderModel? ParentOrder => SellerOrder?.Order;
    public string BuyerName { get; private set; } = string.Empty;
    public string? BuyerEmail { get; private set; }
    public string? BuyerPhone { get; private set; }
    public string PaymentStatus { get; private set; } = string.Empty;
    [TempData]
    public string? StatusMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int sellerOrderId)
    {
        var seller = await _userManager.GetUserAsync(User);
        if (seller is null)
        {
            return Challenge();
        }

        var sellerOrder = await _cartRepository.GetSellerOrderAsync(sellerOrderId, seller.Id);
        if (sellerOrder is null)
        {
            var existing = await _cartRepository.GetSellerOrderByIdAsync(sellerOrderId);
            if (existing is not null)
            {
                return Forbid();
            }

            return NotFound();
        }

        SellerOrder = sellerOrder;
        PaymentStatus = ResolvePaymentStatus(sellerOrder);
        await PopulateBuyerContactAsync(sellerOrder);

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateItemsAsync(
        int sellerOrderId,
        string actionType,
        List<int>? itemIds,
        string? trackingNumber)
    {
        var seller = await _userManager.GetUserAsync(User);
        if (seller is null)
        {
            return Challenge();
        }

        var ids = itemIds ?? new List<int>();
        if (ids.Count == 0)
        {
            ErrorMessage = "Select at least one item.";
            return RedirectToPage(new { sellerOrderId });
        }

        var isCancel = string.Equals(actionType, "cancel", StringComparison.OrdinalIgnoreCase);
        IReadOnlyCollection<int> shippedIds = isCancel ? Array.Empty<int>() : ids;
        IReadOnlyCollection<int> cancelledIds = isCancel ? ids : Array.Empty<int>();

        var result = await _orderStatusService.UpdateItemStatusesAsync(
            sellerOrderId,
            seller.Id,
            shippedIds,
            cancelledIds,
            trackingNumber);

        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error;
        }
        else
        {
            StatusMessage = isCancel ? "Selected items were cancelled." : "Selected items marked as shipped.";
        }

        return RedirectToPage(new { sellerOrderId });
    }

    private async Task PopulateBuyerContactAsync(SellerOrderModel sellerOrder)
    {
        var order = sellerOrder.Order;

        BuyerName = order?.DeliveryRecipientName ?? string.Empty;
        BuyerPhone = order?.DeliveryPhoneNumber;

        if (!string.IsNullOrWhiteSpace(order?.BuyerId))
        {
            var buyer = await _userManager.FindByIdAsync(order.BuyerId);
            if (buyer is not null)
            {
                var fullName = $"{buyer.FirstName} {buyer.LastName}".Trim();
                BuyerName = string.IsNullOrWhiteSpace(fullName) ? BuyerName : fullName;
                BuyerEmail = buyer.Email;
                BuyerPhone = string.IsNullOrWhiteSpace(BuyerPhone) ? buyer.PhoneNumber : BuyerPhone;
            }
        }

        if (string.IsNullOrWhiteSpace(BuyerName))
        {
            BuyerName = order?.BuyerId ?? "Buyer";
        }
    }

    private static string ResolvePaymentStatus(SellerOrderModel order)
    {
        var status = OrderStatusFlow.NormalizeStatus(order.Status);

        if (status.Equals(OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase))
        {
            return "Refunded";
        }

        if (status.Equals(OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return "Payment cancelled";
        }

        if (status.Equals(OrderStatus.New, StringComparison.OrdinalIgnoreCase))
        {
            return "Payment pending";
        }

        return "Paid";
    }
}
