using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public async Task<IActionResult> OnGetAsync(string paymentReference, string status, string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(paymentReference) || string.IsNullOrWhiteSpace(status))
        {
            TempData["PaymentError"] = "Invalid payment response.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        var result = await _paymentProcessingService.HandleCallbackAsync(paymentReference, status, failureReason);
        if (result.NotFound)
        {
            TempData["PaymentError"] = "Payment session was not found or has expired.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        if (result.Success && result.OrderId.HasValue)
        {
            TempData["PaymentStatus"] = "Payment completed successfully.";
            return RedirectToPage("/Buyer/Orders/Details", new { orderId = result.OrderId.Value });
        }

        if (result.Failure)
        {
            TempData["PaymentError"] = "Payment failed. Please try again.";
            if (result.OrderId.HasValue)
            {
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
