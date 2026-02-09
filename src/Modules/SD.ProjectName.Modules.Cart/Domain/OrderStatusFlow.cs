using System;
using System.Collections.Generic;
using System.Linq;

namespace SD.ProjectName.Modules.Cart.Domain;

public static class OrderStatusFlow
{
    private static readonly Dictionary<string, string[]> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        { OrderStatus.New, new[] { OrderStatus.Paid, OrderStatus.Cancelled, OrderStatus.Failed } },
        { OrderStatus.Paid, new[] { OrderStatus.Preparing, OrderStatus.Cancelled, OrderStatus.Refunded } },
        { OrderStatus.Preparing, new[] { OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.Refunded } },
        { OrderStatus.Shipped, new[] { OrderStatus.Delivered, OrderStatus.Refunded } },
        { OrderStatus.Delivered, new[] { OrderStatus.Refunded } },
        { OrderStatus.Cancelled, new[] { OrderStatus.Refunded } },
        { OrderStatus.Refunded, Array.Empty<string>() },
        { OrderStatus.Pending, new[] { OrderStatus.Paid, OrderStatus.Cancelled, OrderStatus.Failed } },
        { OrderStatus.Confirmed, new[] { OrderStatus.Preparing, OrderStatus.Cancelled, OrderStatus.Refunded } }
    };

    public static bool IsValidTransition(string current, string target)
    {
        var normalizedCurrent = NormalizeStatus(current);
        var normalizedTarget = NormalizeStatus(target);

        if (string.Equals(normalizedCurrent, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!AllowedTransitions.TryGetValue(normalizedCurrent, out var allowed))
        {
            return false;
        }

        return allowed.Contains(normalizedTarget, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyCollection<string> NextStatuses(string current) =>
        AllowedTransitions.TryGetValue(NormalizeStatus(current), out var allowed)
            ? allowed
            : Array.Empty<string>();

    public static bool CanCancel(string status)
    {
        var normalized = NormalizeStatus(status);
        return string.Equals(normalized, OrderStatus.New, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, OrderStatus.Paid, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, OrderStatus.Preparing, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsShippedOrBeyond(string status) =>
        string.Equals(status, OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase);

    public static string CalculateOverallStatus(OrderModel order)
    {
        if (order.SubOrders.Count == 0)
        {
            return order.Status;
        }

        var statuses = order.SubOrders.Select(s => NormalizeStatus(s.Status)).ToList();

        if (statuses.Any(s => s.Equals(OrderStatus.Failed, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Failed;
        }

        if (statuses.All(s => s.Equals(OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Refunded;
        }

        if (statuses.Any(s => s.Equals(OrderStatus.Refunded, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Refunded;
        }

        if (statuses.All(s => s.Equals(OrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Cancelled;
        }

        if (statuses.All(s => s.Equals(OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Delivered;
        }

        if (statuses.Any(s => s.Equals(OrderStatus.Delivered, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Shipped;
        }

        if (statuses.Any(s => s.Equals(OrderStatus.Shipped, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Shipped;
        }

        if (statuses.Any(s => s.Equals(OrderStatus.Preparing, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Preparing;
        }

        if (statuses.Any(s => s.Equals(OrderStatus.Paid, StringComparison.OrdinalIgnoreCase)))
        {
            return OrderStatus.Paid;
        }

        return OrderStatus.New;
    }

    public static decimal CalculateRefundedAmount(OrderModel order) =>
        order.SubOrders.Sum(s => s.RefundedAmount);

    public static string NormalizeStatus(string status) =>
        status switch
        {
            var s when s.Equals(OrderStatus.Confirmed, StringComparison.OrdinalIgnoreCase) => OrderStatus.Paid,
            var s when s.Equals(OrderStatus.Pending, StringComparison.OrdinalIgnoreCase) => OrderStatus.New,
            _ => status
        };
}
