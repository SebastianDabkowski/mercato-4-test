using System.Collections.Generic;

namespace SD.ProjectName.Modules.Cart.Domain;

public class PayoutScheduleResult
{
    public List<PayoutSchedule> Schedules { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
