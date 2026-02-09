namespace SD.ProjectName.Modules.Cart.Domain;

public class DeliveryAddressModel
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool SavedToProfile { get; set; }
    public bool IsSelectedForCheckout { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
