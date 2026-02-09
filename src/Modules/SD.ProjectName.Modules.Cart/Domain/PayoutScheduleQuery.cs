using System;
using System.Collections.Generic;

namespace SD.ProjectName.Modules.Cart.Domain;

public class PayoutScheduleQuery
{
    public DateTimeOffset? ScheduledFrom { get; set; }
    public DateTimeOffset? ScheduledTo { get; set; }
    public List<string> Statuses { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
