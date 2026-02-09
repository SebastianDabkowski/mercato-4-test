namespace SD.ProjectName.Modules.Cart.Domain;

public class ShippingRuleModel
{
    public int Id { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string ShippingMethod { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string? DeliveryEstimate { get; set; }
    public string? AllowedRegions { get; set; }
    public string? AllowedCountryCodes { get; set; }
    public decimal? FreeShippingThreshold { get; set; }
    public decimal? PricePerKg { get; set; }
    public decimal? MaxWeightKg { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class ShippingMethods
{
    public const string InPost = "InPost";
    public const string Courier = "Courier";
    public const string SelfPickup = "SelfPickup";
}
