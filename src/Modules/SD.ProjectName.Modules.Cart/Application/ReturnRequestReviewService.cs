using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class ReturnRequestReviewService
{
    private readonly ICartRepository _cartRepository;
    private readonly TimeProvider _timeProvider;
    private readonly Func<int, string, decimal, string?, Task<OrderStatusResult>> _refundHandler;
    private static readonly HashSet<string> AllowedSellerStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ReturnRequestStatus.Requested,
        ReturnRequestStatus.Approved,
        ReturnRequestStatus.PartialProposed,
        ReturnRequestStatus.InfoRequested
    };

    public ReturnRequestReviewService(
        ICartRepository cartRepository,
        OrderStatusService orderStatusService,
        TimeProvider timeProvider,
        Func<int, string, decimal, string?, Task<OrderStatusResult>>? refundHandler = null)
    {
        _cartRepository = cartRepository;
        _timeProvider = timeProvider;
        if (refundHandler is not null)
        {
            _refundHandler = refundHandler;
        }
        else if (orderStatusService is not null)
        {
            _refundHandler = orderStatusService.RefundSellerOrderAsync;
        }
        else
        {
            throw new ArgumentNullException(nameof(orderStatusService));
        }
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

    public async Task<ReturnRequestDecisionResult> ApplySellerResolutionAsync(
        int requestId,
        string sellerId,
        SellerResolutionCommand command)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.Resolution))
        {
            return ReturnRequestDecisionResult.Failed("Select a resolution to continue.");
        }

        var normalizedResolution = command.Resolution.Trim();
        if (!ReturnRequestWorkflow.SellerResolutionToStatus.TryGetValue(normalizedResolution, out var targetStatus))
        {
            return ReturnRequestDecisionResult.Failed("Selected resolution is not supported.");
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

        if (ReturnRequestWorkflow.IsFinalStatus(existing.Status))
        {
            return ReturnRequestDecisionResult.Failed("This case is already resolved.");
        }

        if (existing.SellerOrder is null)
        {
            return ReturnRequestDecisionResult.Failed("Seller order not found for this case.");
        }

        var resolutionNote = string.IsNullOrWhiteSpace(command.ResolutionNote)
            ? null
            : command.ResolutionNote.Trim();

        if (string.Equals(normalizedResolution, ReturnRequestResolution.NoRefund, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(resolutionNote))
        {
            return ReturnRequestDecisionResult.Failed("Provide a reason for choosing no refund.");
        }

        decimal? refundAmount = null;
        var refundStatus = ReturnRequestRefundStatus.NotRequired;
        var refundReference = string.IsNullOrWhiteSpace(command.RefundReference) ? null : command.RefundReference.Trim();

        if (ReturnRequestWorkflow.RequiresRefund(normalizedResolution))
        {
            var outstanding = Math.Max(0m, existing.SellerOrder.TotalAmount - existing.SellerOrder.RefundedAmount);
            if (outstanding <= 0)
            {
                return ReturnRequestDecisionResult.Failed("No refundable balance remains.");
            }

            var requestedAmount = string.Equals(normalizedResolution, ReturnRequestResolution.FullRefund, StringComparison.OrdinalIgnoreCase)
                ? outstanding
                : command.RefundAmount ?? 0m;

            if (requestedAmount <= 0)
            {
                return ReturnRequestDecisionResult.Failed("Enter a refund amount greater than zero.");
            }

            var applied = Math.Min(requestedAmount, outstanding);

            if (command.LinkExistingRefund)
            {
                if (string.IsNullOrWhiteSpace(refundReference))
                {
                    return ReturnRequestDecisionResult.Failed("Enter a refund reference to link the transaction.");
                }

                refundAmount = applied;
                refundStatus = ReturnRequestRefundStatus.Linked;
            }
            else
            {
                var refundResult = await _refundHandler(
                    existing.SellerOrderId,
                    sellerId,
                    applied,
                    resolutionNote);

                if (!refundResult.IsSuccess)
                {
                    return ReturnRequestDecisionResult.Failed(refundResult.Error ?? "Refund could not be processed.");
                }

                refundAmount = applied;
                refundStatus = ReturnRequestRefundStatus.Completed;

                if (refundReference is null)
                {
                    var selection = await _cartRepository.GetPaymentSelectionByOrderIdAsync(existing.OrderId);
                    if (selection is not null && !string.IsNullOrWhiteSpace(selection.ProviderReference))
                    {
                        refundReference = selection.ProviderReference;
                    }
                }
            }
        }

        var updated = await _cartRepository.UpdateReturnRequestStatusAsync(
            requestId,
            sellerId,
            targetStatus,
            _timeProvider.GetUtcNow(),
            normalizedResolution,
            refundAmount,
            refundStatus,
            refundReference,
            resolutionNote);

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

public record SellerResolutionCommand(
    string Resolution,
    decimal? RefundAmount,
    bool LinkExistingRefund,
    string? RefundReference,
    string? ResolutionNote);
