using System.Collections.Generic;

namespace SD.ProjectName.Modules.Cart.Domain;

public class ShippingProvidersOptions
{
    public List<ShippingProviderDefinition> Providers { get; set; } = new();
}

public class ShippingProviderDefinition
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string TrackingUrlTemplate { get; set; } = string.Empty;
    public string? WebhookSecret { get; set; }
    public bool Enabled { get; set; } = true;
    public List<ShippingProviderServiceDefinition> Services { get; set; } = new();
}

public class ShippingProviderServiceDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; }
    public string? DefaultDeliveryEstimate { get; set; }
}
