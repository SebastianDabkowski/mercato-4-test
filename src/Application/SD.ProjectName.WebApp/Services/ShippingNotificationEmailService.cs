using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.WebApp.Services;

public class ShippingNotificationEmailService
{
    private readonly IEmailSender _emailSender;

    public ShippingNotificationEmailService(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public Task SendShippedAsync(string email, SellerOrderModel sellerOrder)
    {
        var culture = CultureInfo.CurrentCulture;
        var subject = $"Order #{sellerOrder.OrderId} shipped";
        var builder = new StringBuilder();

        var sellerName = string.IsNullOrWhiteSpace(sellerOrder.SellerName)
            ? sellerOrder.SellerId
            : sellerOrder.SellerName;
        builder.AppendLine($"<p>Your order #{sellerOrder.OrderId} from {sellerName} has shipped.</p>");

        if (sellerOrder.ShippingSelection is not null)
        {
            builder.AppendLine($"<p>Shipping method: {sellerOrder.ShippingSelection.ShippingMethod} ({sellerOrder.ShippingTotal.ToString("C", culture)})</p>");
        }

        if (!string.IsNullOrWhiteSpace(sellerOrder.TrackingNumber))
        {
            builder.AppendLine($"<p>Tracking number: {sellerOrder.TrackingNumber}</p>");
            if (!string.IsNullOrWhiteSpace(sellerOrder.TrackingCarrier))
            {
                builder.AppendLine($"<p>Carrier: {sellerOrder.TrackingCarrier}</p>");
            }
            if (!string.IsNullOrWhiteSpace(sellerOrder.TrackingUrl))
            {
                builder.AppendLine($"<p>Track your package: <a href=\"{sellerOrder.TrackingUrl}\" target=\"_blank\" rel=\"noopener noreferrer\">{sellerOrder.TrackingUrl}</a></p>");
            }
        }

        if (sellerOrder.Items.Any())
        {
            builder.AppendLine("<p>Items included:</p>");
            builder.AppendLine("<ul>");
            foreach (var item in sellerOrder.Items)
            {
                builder.AppendLine($"<li>{item.ProductName} x{item.Quantity} - {item.UnitPrice.ToString("C", culture)}</li>");
            }
            builder.AppendLine("</ul>");
        }

        return _emailSender.SendEmailAsync(email, subject, builder.ToString());
    }
}
