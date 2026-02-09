namespace SD.ProjectName.Modules.Cart.Domain;

public class CommissionInvoiceResult
{
    public List<CommissionInvoice> Invoices { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
