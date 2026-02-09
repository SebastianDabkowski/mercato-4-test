using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class ShippingIntegrationService
{
    private readonly ICartRepository _cartRepository;
    private readonly OrderStatusService _orderStatusService;
    private readonly IShippingProviderClient _shippingProviderClient;
    private readonly ShippingProvidersOptions _options;

    public ShippingIntegrationService(
        ICartRepository cartRepository,
        OrderStatusService orderStatusService,
        IShippingProviderClient shippingProviderClient,
        IOptions<ShippingProvidersOptions> options)
    {
        _cartRepository = cartRepository;
        _orderStatusService = orderStatusService;
        _shippingProviderClient = shippingProviderClient;
        _options = options.Value ?? new ShippingProvidersOptions();
    }

    public bool UsesIntegratedProvider(SellerOrderModel sellerOrder)
    {
        var method = sellerOrder.ShippingSelection?.ShippingMethod;
        return ResolveProviderForMethod(method) is not null;
    }

    public async Task<EnableProviderResult> EnableProviderForSellerAsync(string sellerId, string providerCode)
    {
        if (string.IsNullOrWhiteSpace(sellerId))
        {
            return EnableProviderResult.Failed("Seller is required.");
        }

        var provider = ResolveProvider(providerCode);
        if (provider is null)
        {
            return EnableProviderResult.Failed("Provider is not configured.");
        }

        foreach (var service in provider.Services)
        {
            var rule = new ShippingRuleModel
            {
                SellerId = sellerId,
                ShippingMethod = service.Name,
                BasePrice = service.DefaultPrice,
                DeliveryEstimate = service.DefaultDeliveryEstimate,
                IsActive = true
            };

            await _cartRepository.UpsertShippingRuleAsync(rule);
        }

        return EnableProviderResult.Success(provider.DisplayName);
    }

    public async Task<ShipmentCreationResult> CreateShipmentAsync(
        int sellerOrderId,
        string sellerId,
        IReadOnlyCollection<int> shippedItemIds,
        CancellationToken cancellationToken = default)
    {
        var sellerOrder = await _cartRepository.GetSellerOrderAsync(sellerOrderId, sellerId);
        if (sellerOrder is null)
        {
            return ShipmentCreationResult.Failure("Sub-order not found.");
        }

        var providerMapping = ResolveProviderForMethod(sellerOrder.ShippingSelection?.ShippingMethod);
        if (providerMapping is null)
        {
            return ShipmentCreationResult.ProviderNotConfigured("Shipping provider not configured for this method.");
        }

        if (shippedItemIds.Count == 0)
        {
            return ShipmentCreationResult.Failure("Select at least one item to ship.");
        }

        var (provider, service) = providerMapping.Value;
        var request = BuildShipmentRequest(sellerOrder, service);
        var providerResult = await _shippingProviderClient.CreateShipmentAsync(request, provider, service, cancellationToken);
        if (!providerResult.IsSuccess)
        {
            return ShipmentCreationResult.Failure(providerResult.Error ?? "Shipment creation failed.", providerResult.IsRetryable);
        }

        var trackingNumber = providerResult.TrackingNumber ?? request.SellerOrderId.ToString();
        var trackingUrl = !string.IsNullOrWhiteSpace(providerResult.TrackingUrl)
            ? providerResult.TrackingUrl
            : BuildTrackingUrl(provider.TrackingUrlTemplate, trackingNumber);
        var carrier = providerResult.Carrier ?? provider.Carrier ?? provider.DisplayName;

        var statusResult = await _orderStatusService.UpdateItemStatusesAsync(
            sellerOrder.Id,
            sellerId,
            shippedItemIds,
            Array.Empty<int>(),
            trackingNumber,
            carrier,
            trackingUrl);

        if (!statusResult.IsSuccess)
        {
            return ShipmentCreationResult.Failure(statusResult.Error ?? "Unable to update shipment status.");
        }

        return ShipmentCreationResult.Success(trackingNumber, carrier, trackingUrl, statusResult);
    }

    public async Task<ProviderStatusResult> ApplyStatusUpdateAsync(ShippingStatusUpdate update)
    {
        var provider = ResolveProvider(update.ProviderCode);
        if (provider is null)
        {
            return ProviderStatusResult.Failed("Provider is not configured.");
        }

        var sellerOrder = await _cartRepository.GetSellerOrderByTrackingAsync(update.TrackingNumber);
        if (sellerOrder is null)
        {
            return ProviderStatusResult.Failed("Shipment not found.");
        }

        var targetStatus = MapProviderStatus(update.Status);
        if (targetStatus is null)
        {
            return ProviderStatusResult.Failed("Unsupported provider status.");
        }

        var trackingUrl = !string.IsNullOrWhiteSpace(update.TrackingUrl)
            ? update.TrackingUrl
            : BuildTrackingUrl(provider.TrackingUrlTemplate, update.TrackingNumber);
        var carrier = update.Carrier ?? sellerOrder.TrackingCarrier ?? provider.Carrier ?? provider.DisplayName;
        var statusResult = await _orderStatusService.UpdateSellerOrderStatusAsync(
            sellerOrder.Id,
            sellerOrder.SellerId,
            targetStatus,
            update.TrackingNumber,
            carrier,
            trackingUrl);

        if (!statusResult.IsSuccess)
        {
            return ProviderStatusResult.Failed(statusResult.Error ?? "Failed to update shipment status.");
        }

        return ProviderStatusResult.Success(statusResult.SubOrderStatus);
    }

    private ShippingProviderShipmentRequest BuildShipmentRequest(SellerOrderModel sellerOrder, ShippingProviderServiceDefinition service)
    {
        var order = sellerOrder.Order;
        var items = sellerOrder.Items.Select(i => new ShippingProviderItem(i.ProductSku, i.ProductName, i.Quantity, i.UnitPrice)).ToList();

        return new ShippingProviderShipmentRequest
        {
            OrderId = sellerOrder.OrderId,
            SellerOrderId = sellerOrder.Id,
            SellerId = sellerOrder.SellerId,
            ShippingMethod = sellerOrder.ShippingSelection?.ShippingMethod ?? service.Name,
            RecipientName = order?.DeliveryRecipientName ?? string.Empty,
            AddressLine1 = order?.DeliveryLine1 ?? string.Empty,
            AddressLine2 = order?.DeliveryLine2,
            City = order?.DeliveryCity ?? string.Empty,
            Region = order?.DeliveryRegion ?? string.Empty,
            PostalCode = order?.DeliveryPostalCode ?? string.Empty,
            CountryCode = order?.DeliveryCountryCode ?? string.Empty,
            Phone = order?.DeliveryPhoneNumber,
            DeclaredValue = Math.Max(0m, sellerOrder.TotalAmount),
            Items = items
        };
    }

    private (ShippingProviderDefinition provider, ShippingProviderServiceDefinition service)? ResolveProviderForMethod(string? shippingMethod)
    {
        if (string.IsNullOrWhiteSpace(shippingMethod))
        {
            return null;
        }

        foreach (var provider in _options.Providers.Where(p => p.Enabled))
        {
            var service = provider.Services.FirstOrDefault(s =>
                string.Equals(s.Name, shippingMethod, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Code, shippingMethod, StringComparison.OrdinalIgnoreCase));

            if (service is not null)
            {
                return (provider, service);
            }
        }

        return null;
    }

    private ShippingProviderDefinition? ResolveProvider(string providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return null;
        }

        return _options.Providers
            .FirstOrDefault(p => p.Enabled && p.Code.Equals(providerCode, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildTrackingUrl(string template, string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(trackingNumber))
        {
            return string.Empty;
        }

        return template.Replace("{trackingNumber}", trackingNumber, StringComparison.OrdinalIgnoreCase);
    }

    private static string? MapProviderStatus(string status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "in_transit" or "shipped" or "out_for_delivery" => OrderStatus.Shipped,
            "delivered" => OrderStatus.Delivered,
            "cancelled" => OrderStatus.Cancelled,
            _ => null
        };
    }
}

public record EnableProviderResult(bool IsSuccess, string? Error = null, string? ProviderName = null)
{
    public static EnableProviderResult Failed(string message) => new(false, message);
    public static EnableProviderResult Success(string? name) => new(true, null, name);
}
