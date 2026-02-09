using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer.Checkout;

[AllowAnonymous]
public class PaymentCallbackModel : PageModel
{
    private readonly PaymentProcessingService _paymentProcessingService;

    public PaymentCallbackModel(PaymentProcessingService paymentProcessingService)
    {
        _paymentProcessingService = paymentProcessingService;
    }

    public async Task<IActionResult> OnGetAsync(
        string paymentReference,
        string status,
        string? failureReason,
        decimal? refundAmount)
    {
        if (string.IsNullOrWhiteSpace(paymentReference) || string.IsNullOrWhiteSpace(status))
        {
            TempData["PaymentError"] = "Invalid payment response.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        var result = await _paymentProcessingService.HandleCallbackAsync(paymentReference, status, failureReason, refundAmount);
        if (result.NotFound)
        {
            TempData["PaymentError"] = "Payment session was not found or has expired.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        if (result.Status == PaymentStatus.Pending && result.OrderId.HasValue)
        {
            TempData["PaymentStatus"] = "Payment is pending confirmation.";
            return RedirectToPage("/Buyer/Orders/Details", new { orderId = result.OrderId.Value });
        }

        if (result.Status == PaymentStatus.Refunded && result.OrderId.HasValue)
        {
            TempData["PaymentStatus"] = string.IsNullOrWhiteSpace(result.FailureReason)
                ? "Payment refunded."
                : $"Refund recorded with issues: {result.FailureReason}";
            return RedirectToPage("/Buyer/Orders/Details", new { orderId = result.OrderId.Value });
        }

        if (result.Success && result.OrderId.HasValue)
        {
            TempData["PaymentStatus"] = "Payment completed successfully.";
            return RedirectToPage("/Buyer/Orders/Details", new { orderId = result.OrderId.Value });
        }

        if (result.Failure)
        {
            var error = string.IsNullOrWhiteSpace(result.FailureReason)
                ? "Payment failed. Please try again."
                : $"Payment failed: {result.FailureReason}";
            TempData["PaymentError"] = error;
            if (result.OrderId.HasValue)
            {
                TempData["OrderError"] = error;
                return RedirectToPage("/Buyer/Orders/Details", new { orderId = result.OrderId.Value });
            }

            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        if (result.Issues.Count > 0)
        {
            TempData["PaymentError"] = "Payment could not be completed. Please review your checkout details.";
        }

        return RedirectToPage("/Buyer/Checkout/Payment");
    }
}
