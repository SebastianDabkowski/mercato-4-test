using System.Collections.Generic;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.WebApp.Services;

public static class PaymentStatusMapper
{
    private static readonly Dictionary<string, PaymentStatus> ProviderStatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "success", PaymentStatus.Paid },
        { "paid", PaymentStatus.Paid },
        { "completed", PaymentStatus.Paid },
        { "authorized", PaymentStatus.Paid },
        { "pending", PaymentStatus.Pending },
        { "processing", PaymentStatus.Pending },
        { "awaiting_payment", PaymentStatus.Pending },
        { "failed", PaymentStatus.Failed },
        { "error", PaymentStatus.Failed },
        { "cancelled", PaymentStatus.Failed },
        { "canceled", PaymentStatus.Failed },
        { "refunded", PaymentStatus.Refunded },
        { "refund", PaymentStatus.Refunded }
    };

    public static PaymentStatus MapProviderStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return PaymentStatus.Failed;
        }

        if (ProviderStatusMap.TryGetValue(status.Trim(), out var mapped))
        {
            return mapped;
        }

        return PaymentStatus.Failed;
    }
}
