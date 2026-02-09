using System.Transactions;
using Microsoft.Extensions.Logging;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.WebApp.Services;

public class PaymentProcessingService
{
    private readonly ICartRepository _cartRepository;
    private readonly PlaceOrder _placeOrder;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentProcessingService> _logger;
    private readonly EscrowService _escrowService;
    private readonly CommissionService _commissionService;

    public PaymentProcessingService(
        ICartRepository cartRepository,
        PlaceOrder placeOrder,
        TimeProvider timeProvider,
        EscrowService escrowService,
        CommissionService commissionService,
        ILogger<PaymentProcessingService> logger)
    {
        _cartRepository = cartRepository;
        _placeOrder = placeOrder;
        _timeProvider = timeProvider;
        _escrowService = escrowService;
        _commissionService = commissionService;
        _logger = logger;
    }

    public async Task<PaymentProcessingResult> HandleCallbackAsync(
        string providerReference,
        string status,
        string? failureReason = null)
    {
        var selection = await _cartRepository.GetPaymentSelectionByReferenceAsync(providerReference);
        if (selection is null)
        {
            _logger.LogWarning("Payment selection not found for reference {Reference}", providerReference);
            return PaymentProcessingResult.CreateNotFound();
        }

        var mappedStatus = PaymentStatusMapper.MapProviderStatus(status);
        return mappedStatus switch
        {
            PaymentStatus.Paid => await HandlePaidAsync(selection, providerReference),
            PaymentStatus.Pending => await HandlePendingAsync(selection, providerReference),
            PaymentStatus.Refunded => await HandleRefundedAsync(selection),
            _ => await HandleFailedAsync(selection, providerReference, failureReason)
        };
    }

    private async Task<PaymentProcessingResult> HandlePaidAsync(PaymentSelectionModel selection, string providerReference)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        if (selection.Status == PaymentStatus.Paid && selection.OrderId.HasValue)
        {
            var existingOrder = await _cartRepository.GetOrderWithSubOrdersAsync(selection.OrderId.Value);
            _commissionService.EnsureCommissionCalculated(existingOrder);
            await _escrowService.EnsureEscrowAsync(existingOrder);
            await _cartRepository.SaveChangesAsync();
            scope.Complete();
            return PaymentProcessingResult.CreateSuccess(selection.OrderId.Value, alreadyProcessed: true);
        }

        selection.Status = PaymentStatus.Paid;
        selection.UpdatedAt = _timeProvider.GetUtcNow();
        await _cartRepository.SaveChangesAsync();

        if (selection.OrderId.HasValue)
        {
            var existingOrder = await _cartRepository.GetOrderWithSubOrdersAsync(selection.OrderId.Value);
            _commissionService.EnsureCommissionCalculated(existingOrder);
            await _escrowService.EnsureEscrowAsync(existingOrder);
            await _cartRepository.SaveChangesAsync();
            scope.Complete();
            return PaymentProcessingResult.CreateSuccess(selection.OrderId.Value, alreadyProcessed: true);
        }

        var orderResult = await _placeOrder.ExecuteAsync(
            selection.BuyerId,
            PaymentStatus.Paid,
            clearPaymentSelection: false);

        if (!orderResult.Success || orderResult.Order is null)
        {
            _logger.LogWarning(
                "Order creation failed for buyer {BuyerId} on payment {Reference}",
                selection.BuyerId,
                providerReference);
            return PaymentProcessingResult.CreateFailed(orderResult.Issues);
        }

        selection.OrderId = orderResult.Order.Id;
        _commissionService.EnsureCommissionCalculated(orderResult.Order);
        await _escrowService.EnsureEscrowAsync(orderResult.Order);
        await _cartRepository.SaveChangesAsync();
        scope.Complete();
        return PaymentProcessingResult.CreateSuccess(orderResult.Order.Id, alreadyProcessed: false);
    }

    private async Task<PaymentProcessingResult> HandlePendingAsync(PaymentSelectionModel selection, string providerReference)
    {
        if (selection.Status == PaymentStatus.Pending && selection.OrderId.HasValue)
        {
            return PaymentProcessingResult.CreatePending(selection.OrderId.Value, alreadyProcessed: true);
        }

        selection.Status = PaymentStatus.Pending;
        selection.UpdatedAt = _timeProvider.GetUtcNow();
        await _cartRepository.SaveChangesAsync();

        if (selection.OrderId.HasValue)
        {
            return PaymentProcessingResult.CreatePending(selection.OrderId.Value, alreadyProcessed: true);
        }

        var orderResult = await _placeOrder.ExecuteAsync(
            selection.BuyerId,
            PaymentStatus.Pending,
            clearPaymentSelection: false);

        if (!orderResult.Success || orderResult.Order is null)
        {
            _logger.LogWarning(
                "Order creation failed for buyer {BuyerId} on payment {Reference}",
                selection.BuyerId,
                providerReference);
            return PaymentProcessingResult.CreateFailed(orderResult.Issues);
        }

        selection.OrderId = orderResult.Order.Id;
        await _cartRepository.SaveChangesAsync();
        return PaymentProcessingResult.CreatePending(orderResult.Order.Id, alreadyProcessed: false);
    }

    private async Task<PaymentProcessingResult> HandleRefundedAsync(PaymentSelectionModel selection)
    {
        var alreadyRefunded = selection.Status == PaymentStatus.Refunded;
        selection.Status = PaymentStatus.Refunded;
        selection.UpdatedAt = _timeProvider.GetUtcNow();
        await _cartRepository.SaveChangesAsync();

        if (selection.OrderId.HasValue)
        {
            return PaymentProcessingResult.CreateRefunded(selection.OrderId.Value, alreadyRefunded);
        }

        return PaymentProcessingResult.CreateRefunded(null, alreadyRefunded);
    }

    private async Task<PaymentProcessingResult> HandleFailedAsync(
        PaymentSelectionModel selection,
        string providerReference,
        string? failureReason)
    {
        if (selection.Status == PaymentStatus.Failed && selection.OrderId.HasValue)
        {
            return PaymentProcessingResult.CreateFailureRecorded(selection.OrderId.Value, alreadyProcessed: true, failureReason);
        }

        selection.Status = PaymentStatus.Failed;
        selection.UpdatedAt = _timeProvider.GetUtcNow();
        await _cartRepository.SaveChangesAsync();

        if (!selection.OrderId.HasValue)
        {
            var orderResult = await _placeOrder.ExecuteAsync(
                selection.BuyerId,
                PaymentStatus.Failed,
                clearPaymentSelection: false);

            if (!orderResult.Success || orderResult.Order is null)
            {
                _logger.LogWarning(
                    "Recording failed payment for buyer {BuyerId} on {Reference} did not produce order",
                    selection.BuyerId,
                    providerReference);
                return PaymentProcessingResult.CreateFailed(orderResult.Issues);
            }

            selection.OrderId = orderResult.Order.Id;
            await _cartRepository.SaveChangesAsync();
            return PaymentProcessingResult.CreateFailureRecorded(orderResult.Order.Id, alreadyProcessed: false, failureReason);
        }

        return PaymentProcessingResult.CreateFailureRecorded(selection.OrderId.Value, alreadyProcessed: true, failureReason);
    }
}

public record PaymentProcessingResult(
    bool Success,
    bool Failure,
    bool NotFound,
    bool AlreadyProcessed,
    int? OrderId,
    string? FailureReason,
    List<CheckoutValidationIssue> Issues,
    PaymentStatus? Status)
{
    public static PaymentProcessingResult CreateSuccess(int orderId, bool alreadyProcessed) =>
        new(true, false, false, alreadyProcessed, orderId, null, new(), PaymentStatus.Paid);

    public static PaymentProcessingResult CreateFailureRecorded(int? orderId, bool alreadyProcessed, string? failureReason = null) =>
        new(false, true, false, alreadyProcessed, orderId, failureReason, new(), PaymentStatus.Failed);

    public static PaymentProcessingResult CreateFailed(List<CheckoutValidationIssue> issues) =>
        new(false, false, false, false, null, null, issues, PaymentStatus.Failed);

    public static PaymentProcessingResult CreateNotFound() =>
        new(false, false, true, false, null, null, new(), null);

    public static PaymentProcessingResult CreatePending(int? orderId, bool alreadyProcessed) =>
        new(false, false, false, alreadyProcessed, orderId, null, new(), PaymentStatus.Pending);

    public static PaymentProcessingResult CreateRefunded(int? orderId, bool alreadyProcessed) =>
        new(true, false, false, alreadyProcessed, orderId, null, new(), PaymentStatus.Refunded);
}
