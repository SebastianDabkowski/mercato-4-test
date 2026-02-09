using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer.Cases;

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

    public ReturnRequestModel? Case { get; private set; }
    public PaymentSelectionModel? PaymentSelection { get; private set; }
    public SellerOrderModel? SellerOrder => Case?.SellerOrder;
    public OrderModel? Order => Case?.Order;

    public async Task<IActionResult> OnGetAsync(int caseId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        Case = await _cartRepository.GetReturnRequestAsync(caseId, buyerId);
        if (Case is null)
        {
            var existing = await _cartRepository.GetReturnRequestByIdAsync(caseId);
            if (existing is not null)
            {
                return Forbid();
            }

            return NotFound();
        }

        PaymentSelection = await _cartRepository.GetPaymentSelectionByOrderIdAsync(Case.OrderId);
        return Page();
    }

    public string GetCaseStatusLabel(string status) =>
        status?.ToLowerInvariant() switch
        {
            ReturnRequestStatus.Requested => "Pending seller review",
            ReturnRequestStatus.Approved => "Approved",
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
            _ => "bg-warning text-dark"
        };

    public string GetCaseTypeLabel(string requestType) =>
        string.Equals(requestType, ReturnRequestType.Complaint, StringComparison.OrdinalIgnoreCase)
            ? "Complaint"
            : "Return";

    public string GetResolutionSummary(string status) =>
        status?.ToLowerInvariant() switch
        {
            ReturnRequestStatus.Approved => "Seller approved this request.",
            ReturnRequestStatus.Rejected => "Seller rejected this request.",
            ReturnRequestStatus.Completed => "Case marked as completed.",
            _ => "Awaiting seller decision."
        };
}
