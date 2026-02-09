namespace SD.ProjectName.Modules.Cart.Domain;

public class CommissionInvoice
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public string Status { get; set; } = CommissionInvoiceStatus.Issued;
    public bool IsCreditNote { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "PLN";
    public List<CommissionInvoiceLine> Lines { get; set; } = new();
}

public class CommissionInvoiceLine
{
    public int Id { get; set; }
    public int CommissionInvoiceId { get; set; }
    public CommissionInvoice? CommissionInvoice { get; set; }
    public int EscrowLedgerEntryId { get; set; }
    public EscrowLedgerEntry? EscrowLedgerEntry { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsCorrection { get; set; }
}

public static class CommissionInvoiceStatus
{
    public const string Issued = "issued";
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";
}
