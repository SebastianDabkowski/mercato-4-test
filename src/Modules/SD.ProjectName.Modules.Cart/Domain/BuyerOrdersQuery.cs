using System;
using System.Collections.Generic;

namespace SD.ProjectName.Modules.Cart.Domain;

public class BuyerOrdersQuery
{
    public IReadOnlyCollection<string> Statuses { get; init; } = Array.Empty<string>();
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public string? SellerId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class BuyerOrdersResult
{
    public List<OrderModel> Orders { get; init; } = new();
    public int TotalCount { get; init; }
    public List<SellerSummary> Sellers { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public class SellerSummary
{
    public string SellerId { get; init; } = string.Empty;
    public string SellerName { get; init; } = string.Empty;
}
