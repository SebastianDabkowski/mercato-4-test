using System;
using System.Threading;
using System.Threading.Tasks;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Modules.Cart.Infrastructure.Shipping;

public class StubShippingProviderClient : IShippingProviderClient
{
    private readonly TimeProvider _timeProvider;

    public StubShippingProviderClient(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task<ShippingProviderShipmentResult> CreateShipmentAsync(
        ShippingProviderShipmentRequest request,
        ShippingProviderDefinition provider,
        ShippingProviderServiceDefinition service,
        CancellationToken cancellationToken = default)
    {
        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var trackingNumber = $"{provider.Code}-{service.Code}-{timestamp}";
        var trackingUrl = BuildTrackingUrl(provider.TrackingUrlTemplate, trackingNumber);
        var carrier = string.IsNullOrWhiteSpace(provider.Carrier) ? provider.DisplayName : provider.Carrier;

        return Task.FromResult(new ShippingProviderShipmentResult(
            true,
            trackingNumber,
            trackingUrl,
            carrier));
    }

    private static string BuildTrackingUrl(string template, string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return template.Replace("{trackingNumber}", trackingNumber, StringComparison.OrdinalIgnoreCase);
    }
}
