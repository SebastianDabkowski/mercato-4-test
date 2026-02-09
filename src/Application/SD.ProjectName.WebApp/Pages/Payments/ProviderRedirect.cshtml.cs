using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace SD.ProjectName.WebApp.Pages.Payments;

[AllowAnonymous]
public class ProviderRedirectModel : PageModel
{
    private readonly ILogger<ProviderRedirectModel> _logger;

    public ProviderRedirectModel(ILogger<ProviderRedirectModel> logger)
    {
        _logger = logger;
    }

    public IActionResult OnGet(string paymentReference, string method, string? blikCode)
    {
        if (string.IsNullOrWhiteSpace(paymentReference) || string.IsNullOrWhiteSpace(method))
        {
            TempData["PaymentError"] = "Payment session is missing or expired.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        var status = DetermineStatus(method, blikCode);
        var callbackUrl = Url.Page("/Buyer/Checkout/PaymentCallback", new
        {
            paymentReference,
            status,
            failureReason = status == "failed" ? "invalid-blik-code" : null
        });

        if (callbackUrl is null)
        {
            _logger.LogWarning("Unable to resolve payment callback URL for reference {Reference}", paymentReference);
            TempData["PaymentError"] = "Unable to complete payment at this time.";
            return RedirectToPage("/Buyer/Checkout/Payment");
        }

        return Redirect(callbackUrl);
    }

    private static string DetermineStatus(string method, string? blikCode)
    {
        if (string.Equals(method, "BLIK", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedCode = blikCode?.Trim() ?? string.Empty;
            if (normalizedCode.Length != 6 || !normalizedCode.All(char.IsDigit))
            {
                return "failed";
            }
        }

        return "success";
    }
}
