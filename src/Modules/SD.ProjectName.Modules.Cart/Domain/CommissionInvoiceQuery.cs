namespace SD.ProjectName.Modules.Cart.Domain;

public class CommissionInvoiceQuery
{
    public DateTimeOffset? PeriodFrom { get; set; }
    public DateTimeOffset? PeriodTo { get; set; }
    public List<string> Statuses { get; set; } = new();
    public bool IncludeCreditNotes { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
