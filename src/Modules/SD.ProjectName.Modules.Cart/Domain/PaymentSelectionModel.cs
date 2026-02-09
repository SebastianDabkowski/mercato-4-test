namespace SD.ProjectName.Modules.Cart.Domain;

public class PaymentSelectionModel
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string ProviderReference { get; set; } = string.Empty;
    public int? OrderId { get; set; }
}

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Failed = 2,
    Cancelled = 3
}
