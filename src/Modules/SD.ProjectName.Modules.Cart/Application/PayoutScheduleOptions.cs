namespace SD.ProjectName.Modules.Cart.Application;

public class PayoutScheduleOptions
{
    public decimal MinimumPayoutAmount { get; set; } = 50m;
    public int ScheduleIntervalDays { get; set; } = 7;
}
