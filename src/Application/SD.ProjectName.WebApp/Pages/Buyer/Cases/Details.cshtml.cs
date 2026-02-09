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
    private readonly TimeProvider _timeProvider;

    public DetailsModel(ICartIdentityService cartIdentityService, ICartRepository cartRepository, TimeProvider timeProvider)
    {
        _cartIdentityService = cartIdentityService;
        _cartRepository = cartRepository;
        _timeProvider = timeProvider;
    }

    public ReturnRequestModel? Case { get; private set; }
    public PaymentSelectionModel? PaymentSelection { get; private set; }
    public SellerOrderModel? SellerOrder => Case?.SellerOrder;
    public OrderModel? Order => Case?.Order;
    public List<ReturnRequestMessageModel> Thread { get; private set; } = new();

    [BindProperty]
    public string MessageBody { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int caseId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var result = await LoadCaseAsync(caseId, buyerId);
        if (result is not null)
        {
            return result;
        }

        await _cartRepository.MarkBuyerMessagesReadAsync(caseId, buyerId);
        PaymentSelection = await _cartRepository.GetPaymentSelectionByOrderIdAsync(Case!.OrderId);
        return Page();
    }

    public async Task<IActionResult> OnPostMessageAsync(int caseId)
    {
        var buyerId = _cartIdentityService.GetOrCreateBuyerId();
        var trimmed = MessageBody?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ModelState.AddModelError(nameof(MessageBody), "Please enter a message.");
        }

        var loadResult = await LoadCaseAsync(caseId, buyerId);
        if (loadResult is not null)
        {
            return loadResult;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var created = await _cartRepository.AddBuyerReturnRequestMessageAsync(caseId, buyerId, trimmed, _timeProvider.GetUtcNow());
        if (created is null)
        {
            return Forbid();
        }

        await _cartRepository.MarkBuyerMessagesReadAsync(caseId, buyerId);
        return RedirectToPage(new { caseId });
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

    public string GetResolutionSummary(string status) =>
        status?.ToLowerInvariant() switch
        {
            ReturnRequestStatus.Approved => "Seller approved this request.",
            ReturnRequestStatus.Rejected => "Seller rejected this request.",
            ReturnRequestStatus.PartialProposed => "Seller proposed a partial solution.",
            ReturnRequestStatus.InfoRequested => "Seller requested more information.",
            ReturnRequestStatus.Completed => "Case marked as completed.",
            _ => "Awaiting seller decision."
        };

    public string GetSenderLabel(ReturnRequestMessageModel message)
    {
        return message.SenderRole?.ToLowerInvariant() switch
        {
            ReturnRequestMessageSender.Buyer => "You",
            ReturnRequestMessageSender.Seller => string.IsNullOrWhiteSpace(SellerOrder?.SellerName) ? "Seller" : SellerOrder.SellerName,
            _ => "System"
        };
    }

    private async Task<IActionResult?> LoadCaseAsync(int caseId, string buyerId)
    {
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
}
