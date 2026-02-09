using System;
using System.Collections.Generic;

namespace SD.ProjectName.Modules.Cart.Domain;

public class SellerReturnRequestsQuery
{
    public IReadOnlyCollection<string> Statuses { get; init; } = Array.Empty<string>();
    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public class SellerReturnRequestsResult
{
    public List<ReturnRequestModel> Requests { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
