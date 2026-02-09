using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Modules.Cart.Application;

public record ShippingProviderItem(string Sku, string Name, int Quantity, decimal UnitPrice);

public record ShippingProviderShipmentRequest
{
    public int OrderId { get; init; }
    public int SellerOrderId { get; init; }
    public string SellerId { get; init; } = string.Empty;
    public string ShippingMethod { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public decimal DeclaredValue { get; init; }
    public IReadOnlyCollection<ShippingProviderItem> Items { get; init; } = Array.Empty<ShippingProviderItem>();
}

public record ShippingProviderShipmentResult(
    bool IsSuccess,
    string? TrackingNumber = null,
    string? TrackingUrl = null,
    string? Carrier = null,
    string? Error = null,
    bool IsRetryable = false,
    byte[]? LabelContent = null,
    string? LabelContentType = null,
    string? LabelFileName = null);

public record ShippingStatusUpdate(
    string ProviderCode,
    string TrackingNumber,
    string Status,
    string? TrackingUrl = null,
    string? Carrier = null);

public record ShipmentCreationResult(
    bool IsSuccess,
    bool IsProviderIntegrated,
    string? Error = null,
    string? TrackingNumber = null,
    string? TrackingCarrier = null,
    string? TrackingUrl = null,
    bool IsRetryable = false,
    OrderStatusResult? StatusResult = null)
{
    public static ShipmentCreationResult ProviderNotConfigured(string message) =>
        new(false, false, message);

    public static ShipmentCreationResult Failure(string message, bool isRetryable = false) =>
        new(false, true, message, null, null, null, isRetryable);

    public static ShipmentCreationResult Success(
        string trackingNumber,
        string trackingCarrier,
        string trackingUrl,
        OrderStatusResult statusResult) =>
        new(true, true, null, trackingNumber, trackingCarrier, trackingUrl, false, statusResult);
}

public record ProviderStatusResult(bool IsSuccess, string? Error = null, string? SubOrderStatus = null)
{
    public static ProviderStatusResult Failed(string message) => new(false, message);
    public static ProviderStatusResult Success(string? status) => new(true, null, status);
}

public interface IShippingProviderClient
{
    Task<ShippingProviderShipmentResult> CreateShipmentAsync(
        ShippingProviderShipmentRequest request,
        ShippingProviderDefinition provider,
        ShippingProviderServiceDefinition service,
        CancellationToken cancellationToken = default);
}
