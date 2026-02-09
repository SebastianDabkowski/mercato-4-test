using System;
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
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Seller.Cases;

[Authorize(Roles = IdentityRoles.Seller)]
public class DetailsModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICartRepository _cartRepository;
    private readonly ReturnRequestReviewService _returnRequestReviewService;
    private readonly ReturnRequestNotificationEmailService _returnRequestNotificationEmailService;

    public DetailsModel(
        UserManager<ApplicationUser> userManager,
        ICartRepository cartRepository,
        ReturnRequestReviewService returnRequestReviewService,
        ReturnRequestNotificationEmailService returnRequestNotificationEmailService)
    {
        _userManager = userManager;
        _cartRepository = cartRepository;
        _returnRequestReviewService = returnRequestReviewService;
        _returnRequestNotificationEmailService = returnRequestNotificationEmailService;
    }

    public ReturnRequestModel? Case { get; private set; }
    public SellerOrderModel? SellerOrder => Case?.SellerOrder;
    public OrderModel? Order => Case?.Order;
    public bool CanTakeAction =>
        Case is not null &&
        string.Equals(Case.Status, ReturnRequestStatus.Requested, StringComparison.OrdinalIgnoreCase);

    [TempData]
    public string? StatusMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int caseId)
    {
        var seller = await _userManager.GetUserAsync(User);
        if (seller is null)
        {
            return Challenge();
        }

        var result = await LoadCaseAsync(caseId, seller.Id);
        return result ?? Page();
    }

    public async Task<IActionResult> OnPostDecideAsync(int caseId, string decision)
    {
        var seller = await _userManager.GetUserAsync(User);
        if (seller is null)
        {
            return Challenge();
        }

        var decisionResult = await _returnRequestReviewService.ApplySellerDecisionAsync(caseId, seller.Id, decision);
        if (!decisionResult.IsSuccess)
        {
            if (decisionResult.IsForbidden)
            {
                return Forbid();
            }

            ErrorMessage = decisionResult.Error;
            var reload = await LoadCaseAsync(caseId, seller.Id);
            return reload ?? Page();
        }

        var loadResult = await LoadCaseAsync(caseId, seller.Id);
        if (loadResult is not null)
        {
            return loadResult;
        }

        StatusMessage = $"Case updated to {GetCaseStatusLabel(Case!.Status)}.";
        await NotifyBuyerAsync(Case!);

        return RedirectToPage(new { caseId });
    }

    private async Task<IActionResult?> LoadCaseAsync(int caseId, string sellerId)
    {
        Case = await _cartRepository.GetReturnRequestForSellerAsync(caseId, sellerId);
        if (Case is null)
        {
            var existing = await _cartRepository.GetReturnRequestByIdAsync(caseId);
            if (existing is not null)
            {
                return Forbid();
            }

            return NotFound();
        }

        return null;
    }

    private async Task NotifyBuyerAsync(ReturnRequestModel request)
    {
        var buyerId = request.Order?.BuyerId;
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            return;
        }

        var buyer = await _userManager.FindByIdAsync(buyerId);
        if (buyer?.Email is null)
        {
            return;
        }

        var sellerName = !string.IsNullOrWhiteSpace(request.SellerOrder?.SellerName)
            ? request.SellerOrder!.SellerName
            : request.SellerOrder?.SellerId ?? "Seller";

        var statusLabel = GetCaseStatusLabel(request.Status);
        await _returnRequestNotificationEmailService.SendSellerDecisionAsync(buyer.Email, request, sellerName, statusLabel);
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

    public string GetBuyerAlias() => Order?.BuyerId?.Length > 6
        ? $"Buyer {Order.BuyerId[^6..]}"
        : Order?.BuyerId ?? "Buyer";
}
