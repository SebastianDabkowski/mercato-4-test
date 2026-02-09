using System;
using System.Collections.Generic;

namespace SD.ProjectName.Modules.Cart.Domain;

public class SellerOrdersQuery
{
    public IReadOnlyCollection<string> Statuses { get; init; } = Array.Empty<string>();
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public string? BuyerId { get; init; }
    public bool WithoutTracking { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class SellerOrdersResult
{
    public List<SellerOrderModel> Orders { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
