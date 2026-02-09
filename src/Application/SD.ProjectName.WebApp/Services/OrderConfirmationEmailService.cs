using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Localization;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.WebApp.Services;

public class OrderConfirmationEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly IStringLocalizer<OrderConfirmationEmailService> _localizer;

    public OrderConfirmationEmailService(
        IEmailSender emailSender,
        IStringLocalizer<OrderConfirmationEmailService> localizer)
    {
        _emailSender = emailSender;
        _localizer = localizer;
    }

    public Task SendAsync(string email, OrderModel order)
    {
        var culture = CultureInfo.CurrentCulture;
        var subject = _localizer["OrderConfirmationSubject", order.Id];
        var builder = new StringBuilder();

        builder.AppendLine($"<p>{_localizer["ThankYouBody", order.Id]}</p>");
        builder.AppendLine("<h3>" + _localizer["OrderSummary"] + "</h3>");
        builder.AppendLine("<ul>");
        foreach (var item in order.Items)
        {
            builder.AppendLine($"<li>{item.ProductName} x{item.Quantity} - {item.UnitPrice.ToString("C", culture)}</li>");
        }
        builder.AppendLine("</ul>");
        builder.AppendLine($"<p>{_localizer["ItemsSubtotal"]}: {order.ItemsSubtotal.ToString("C", culture)}</p>");
        if (order.DiscountTotal > 0)
        {
            var promoLabel = string.IsNullOrWhiteSpace(order.PromoCode)
                ? _localizer["PromoDiscount"]
                : _localizer["PromoDiscount"] + $" ({order.PromoCode})";
            builder.AppendLine($"<p>{promoLabel}: -{order.DiscountTotal.ToString("C", culture)}</p>");
        }
        builder.AppendLine($"<p>{_localizer["ShippingTotal"]}: {order.ShippingTotal.ToString("C", culture)}</p>");
        builder.AppendLine($"<p><strong>{_localizer["TotalPaid"]}: {order.TotalAmount.ToString("C", culture)}</strong></p>");

        var shippingMethods = order.ShippingSelections.Any()
            ? string.Join(", ", order.ShippingSelections.Select(s => $"{s.SellerName}: {s.ShippingMethod} ({s.Cost.ToString("C", culture)})"))
            : _localizer["ShippingMethodNotAvailable"];
        builder.AppendLine($"<p>{_localizer["ShippingMethods"]}: {shippingMethods}</p>");

        var estimatedDelivery = order.ShippingSelections
            .Where(s => s.EstimatedDeliveryDate.HasValue)
            .OrderBy(s => s.EstimatedDeliveryDate)
            .Select(s => s.EstimatedDeliveryDate?.ToString("D", culture))
            .FirstOrDefault();
        builder.AppendLine($"<p>{_localizer["EstimatedDelivery"]}: {estimatedDelivery ?? _localizer["EstimatedDeliveryUnavailable"]}</p>");

        builder.AppendLine("<h3>" + _localizer["DeliveryAddress"] + "</h3>");
        builder.AppendLine($"<p>{order.DeliveryRecipientName}<br/>{order.DeliveryLine1}<br/>");
        if (!string.IsNullOrWhiteSpace(order.DeliveryLine2))
        {
            builder.AppendLine($"{order.DeliveryLine2}<br/>");
        }
        builder.AppendLine($"{order.DeliveryPostalCode} {order.DeliveryCity}<br/>{order.DeliveryRegion}, {order.DeliveryCountryCode}</p>");

        return _emailSender.SendEmailAsync(email, subject, builder.ToString());
    }
}
