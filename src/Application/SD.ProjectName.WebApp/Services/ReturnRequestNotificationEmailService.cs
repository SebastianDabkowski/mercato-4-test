using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.WebApp.Services;

public class ReturnRequestNotificationEmailService
{
    private readonly IEmailSender _emailSender;

    public ReturnRequestNotificationEmailService(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public Task SendSellerDecisionAsync(string email, ReturnRequestModel request, string sellerDisplayName, string statusLabel)
    {
        var subject = $"Update on your case #{request.Id}";
        var builder = new StringBuilder();
        var typeLabel = GetCaseTypeLabel(request.RequestType);

        builder.AppendLine($"<p>Your {typeLabel.ToLower(CultureInfo.CurrentCulture)} request for order #{request.OrderId} was updated by {sellerDisplayName}.</p>");
        builder.AppendLine($"<p>Current status: {statusLabel}</p>");

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            builder.AppendLine("<p>Your submitted details:</p>");
            builder.AppendLine($"<blockquote>{request.Description}</blockquote>");
        }

        builder.AppendLine($"<p>Case ID: #{request.Id}</p>");

        return _emailSender.SendEmailAsync(email, subject, builder.ToString());
    }

    private static string GetCaseTypeLabel(string requestType) =>
        string.Equals(requestType, ReturnRequestType.Complaint, StringComparison.OrdinalIgnoreCase)
            ? "Complaint"
            : "Return";
}
