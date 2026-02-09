namespace SD.ProjectName.Modules.Cart.Application;

public class CommissionInvoiceOptions
{
    public string NumberPrefix { get; set; } = "CI";
    public decimal DefaultTaxRate { get; set; } = 0.23m;
    public Dictionary<string, decimal> SellerTaxOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Currency { get; set; } = "PLN";
}
