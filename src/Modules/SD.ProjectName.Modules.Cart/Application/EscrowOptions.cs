namespace SD.ProjectName.Modules.Cart.Application;

public class EscrowOptions
{
    public decimal CommissionRate { get; set; } = 0.01m;
    public int PayoutEligibilityDelayDays { get; set; } = 7;
}
