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
    private readonly TimeProvider _timeProvider;

    public DetailsModel(
        UserManager<ApplicationUser> userManager,
        ICartRepository cartRepository,
        ReturnRequestReviewService returnRequestReviewService,
        ReturnRequestNotificationEmailService returnRequestNotificationEmailService,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _cartRepository = cartRepository;
        _returnRequestReviewService = returnRequestReviewService;
        _returnRequestNotificationEmailService = returnRequestNotificationEmailService;
        _timeProvider = timeProvider;
    }

    public ReturnRequestModel? Case { get; private set; }
    public SellerOrderModel? SellerOrder => Case?.SellerOrder;
    public OrderModel? Order => Case?.Order;
    public bool CanTakeAction =>
        Case is not null &&
        !ReturnRequestWorkflow.IsFinalStatus(Case.Status);
    public List<ReturnRequestMessageModel> Thread { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public string MessageBody { get; set; } = string.Empty;
    [BindProperty]
    public string ResolutionOption { get; set; } = ReturnRequestResolution.FullRefund;
    [BindProperty]
    public decimal? ResolutionRefundAmount { get; set; }
    [BindProperty]
    public bool LinkExistingRefund { get; set; }
    [BindProperty]
    public string RefundReference { get; set; } = string.Empty;
    [BindProperty]
    public string ResolutionNote { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int caseId)
    {
        var seller = await _userManager.GetUserAsync(User);
        if (seller is null)
        {
            return Challenge();
        }

        var result = await LoadCaseAsync(caseId, seller.Id);
        if (result is not null)
        {
            return result;
        }

        await _cartRepository.MarkSellerMessagesReadAsync(caseId, seller.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostDecideAsync(int caseId)
    {
        var seller = await _userManager.GetUserAsync(User);
        if (seller is null)
        {
            return Challenge();
        }

        var command = new SellerResolutionCommand(
            ResolutionOption,
            ResolutionRefundAmount,
            LinkExistingRefund,
            RefundReference,
            ResolutionNote);

        var decisionResult = await _returnRequestReviewService.ApplySellerResolutionAsync(caseId, seller.Id, command);
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

        StatusMessage = $"Case resolved: {GetCaseStatusLabel(Case!.Status, Case.Resolution)}.";
        await NotifyBuyerAsync(Case!);

        return RedirectToPage(new { caseId });
    }

    public async Task<IActionResult> OnPostMessageAsync(int caseId)
    {
        var seller = await _userManager.GetUserAsync(User);
        if (seller is null)
        {
            return Challenge();
        }

        var trimmed = MessageBody?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ModelState.AddModelError(nameof(MessageBody), "Please enter a message.");
        }

        var result = await LoadCaseAsync(caseId, seller.Id);
        if (result is not null)
        {
            return result;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var created = await _cartRepository.AddSellerReturnRequestMessageAsync(caseId, seller.Id, trimmed, _timeProvider.GetUtcNow());
        if (created is null)
        {
            return Forbid();
        }

        await _cartRepository.MarkSellerMessagesReadAsync(caseId, seller.Id);
        StatusMessage = "Message sent.";
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

        Thread = (Case.Messages ?? new List<ReturnRequestMessageModel>())
            .OrderBy(m => m.CreatedAt)
            .ToList();

        if (!string.IsNullOrWhiteSpace(Case.Description))
        {
            Thread.Insert(0, new ReturnRequestMessageModel
            {
                ReturnRequestId = Case.Id,
                SenderRole = ReturnRequestMessageSender.Buyer,
                SenderId = Case.BuyerId,
                Body = Case.Description,
                CreatedAt = Case.RequestedAt
            });
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

        var statusLabel = GetCaseStatusLabel(request.Status, request.Resolution);
        await _returnRequestNotificationEmailService.SendSellerDecisionAsync(buyer.Email, request, sellerName, statusLabel);
    }

    public string GetCaseStatusLabel(string status, string? resolution = null) =>
        status?.ToLowerInvariant() switch
        {
            ReturnRequestStatus.Completed when !string.IsNullOrWhiteSpace(resolution) =>
                $"Resolved: {ReturnRequestWorkflow.GetResolutionLabel(resolution)}",
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

    public string GetResolutionLabel(ReturnRequestModel request) =>
        ReturnRequestWorkflow.GetResolutionLabel(request.Resolution);

    public string GetRefundStatusLabel(ReturnRequestModel request) =>
        request.RefundStatus?.ToLowerInvariant() switch
        {
            ReturnRequestRefundStatus.Completed => "Refund completed",
            ReturnRequestRefundStatus.Linked => "Refund linked",
            ReturnRequestRefundStatus.Pending => "Refund pending",
            ReturnRequestRefundStatus.Failed => "Refund failed",
            _ => "Refund not required"
        };

    public string GetCaseTypeLabel(string requestType) =>
        string.Equals(requestType, ReturnRequestType.Complaint, StringComparison.OrdinalIgnoreCase)
            ? "Complaint"
            : "Return";

    public string GetBuyerAlias() => Order?.BuyerId?.Length > 6
        ? $"Buyer {Order.BuyerId[^6..]}"
        : Order?.BuyerId ?? "Buyer";

    public string GetSenderLabel(ReturnRequestMessageModel message)
    {
        return message.SenderRole?.ToLowerInvariant() switch
        {
            ReturnRequestMessageSender.Seller => "You",
            ReturnRequestMessageSender.Buyer => GetBuyerAlias(),
            _ => "System"
        };
    }
}
