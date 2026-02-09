using System;

namespace SD.ProjectName.Modules.Cart.Domain;

public class ShippingStatusHistory
{
    public int Id { get; set; }
    public int SellerOrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ChangedBy { get; set; }
    public string? ChangedByRole { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingCarrier { get; set; }
    public string? TrackingUrl { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public SellerOrderModel? SellerOrder { get; set; }
}
