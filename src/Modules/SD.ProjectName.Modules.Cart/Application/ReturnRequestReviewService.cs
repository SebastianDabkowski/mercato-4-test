using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class ReturnRequestReviewService
{
    private readonly ICartRepository _cartRepository;
    private readonly TimeProvider _timeProvider;
    private static readonly HashSet<string> AllowedSellerStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ReturnRequestStatus.Requested
    };

    public ReturnRequestReviewService(ICartRepository cartRepository, TimeProvider timeProvider)
    {
        _cartRepository = cartRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ReturnRequestDecisionResult> ApplySellerDecisionAsync(int requestId, string sellerId, string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return ReturnRequestDecisionResult.Failed("Select an action to continue.");
        }

        var normalizedAction = action.Trim();
        if (!ReturnRequestWorkflow.SellerActionToStatus.TryGetValue(normalizedAction, out var targetStatus))
        {
            return ReturnRequestDecisionResult.Failed("Selected action is not supported.");
        }

        var existing = await _cartRepository.GetReturnRequestForSellerAsync(requestId, sellerId);
        if (existing is null)
        {
            var other = await _cartRepository.GetReturnRequestByIdAsync(requestId);
            if (other is not null)
            {
                return ReturnRequestDecisionResult.Forbidden("You cannot modify this case.");
            }

            return ReturnRequestDecisionResult.NotFound("Case not found.");
        }

        if (!AllowedSellerStatuses.Contains(existing.Status))
        {
            return ReturnRequestDecisionResult.Failed("This case is no longer awaiting seller review.");
        }

        var updated = await _cartRepository.UpdateReturnRequestStatusAsync(
            requestId,
            sellerId,
            targetStatus,
            _timeProvider.GetUtcNow());

        if (updated is null)
        {
            return ReturnRequestDecisionResult.NotFound("Case not found.");
        }

        return ReturnRequestDecisionResult.Success(updated);
    }
}

public record ReturnRequestDecisionResult(bool IsSuccess, string? Error, ReturnRequestModel? Request, bool IsForbidden = false)
{
    public static ReturnRequestDecisionResult Success(ReturnRequestModel request) => new(true, null, request, false);
    public static ReturnRequestDecisionResult Failed(string message) => new(false, message, null, false);
    public static ReturnRequestDecisionResult NotFound(string message) => new(false, message, null, false);
    public static ReturnRequestDecisionResult Forbidden(string message) => new(false, message, null, true);
}
