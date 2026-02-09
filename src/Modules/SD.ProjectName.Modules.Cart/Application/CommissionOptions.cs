namespace SD.ProjectName.Modules.Cart.Application;

public class CommissionOptions
{
    public decimal DefaultRate { get; set; } = 0.01m;
    public Dictionary<string, decimal> SellerOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> CategoryOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
