namespace SD.ProjectName.Modules.Cart.Domain;

public class EscrowLedgerEntry
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SellerOrderId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public decimal HeldAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal SellerPayoutAmount { get; set; }
    public string Status { get; set; } = EscrowLedgerStatus.Held;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
    public DateTimeOffset PayoutEligibleAt { get; set; }
}

public static class EscrowLedgerStatus
{
    public const string Held = "held";
    public const string ReleasedToBuyer = "released-buyer";
    public const string ReleasedToSeller = "released-seller";
}
