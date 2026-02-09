namespace SD.ProjectName.Modules.Cart.Domain;

public class ShippingSelectionModel
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string ShippingMethod { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string? DeliveryEstimate { get; set; }
    public DateTimeOffset SelectedAt { get; set; }
}
