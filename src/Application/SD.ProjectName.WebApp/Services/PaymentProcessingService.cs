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

    public PaymentProcessingService(
        ICartRepository cartRepository,
        PlaceOrder placeOrder,
        TimeProvider timeProvider,
        ILogger<PaymentProcessingService> logger)
    {
        _cartRepository = cartRepository;
        _placeOrder = placeOrder;
        _timeProvider = timeProvider;
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

        var normalizedStatus = status.ToLowerInvariant();
        if (normalizedStatus == "success")
        {
            if (selection.Status == PaymentStatus.Authorized && selection.OrderId.HasValue)
            {
                return PaymentProcessingResult.CreateSuccess(selection.OrderId.Value, alreadyProcessed: true);
            }

            selection.Status = PaymentStatus.Authorized;
            selection.UpdatedAt = _timeProvider.GetUtcNow();
            await _cartRepository.SaveChangesAsync();

            if (selection.OrderId.HasValue)
            {
                return PaymentProcessingResult.CreateSuccess(selection.OrderId.Value, alreadyProcessed: true);
            }

            var orderResult = await _placeOrder.ExecuteAsync(
                selection.BuyerId,
                PaymentStatus.Authorized,
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
            return PaymentProcessingResult.CreateSuccess(orderResult.Order.Id, alreadyProcessed: false);
        }

        if (selection.Status == PaymentStatus.Failed && selection.OrderId.HasValue)
        {
            return PaymentProcessingResult.CreateFailureRecorded(selection.OrderId.Value, alreadyProcessed: true);
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
    List<CheckoutValidationIssue> Issues)
{
    public static PaymentProcessingResult CreateSuccess(int orderId, bool alreadyProcessed) =>
        new(true, false, false, alreadyProcessed, orderId, null, new());

    public static PaymentProcessingResult CreateFailureRecorded(int? orderId, bool alreadyProcessed, string? failureReason = null) =>
        new(false, true, false, alreadyProcessed, orderId, failureReason, new());

    public static PaymentProcessingResult CreateFailed(List<CheckoutValidationIssue> issues) =>
        new(false, false, false, false, null, null, issues);

    public static PaymentProcessingResult CreateNotFound() =>
        new(false, false, true, false, null, null, new());
}
