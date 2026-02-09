namespace SD.ProjectName.Modules.Cart.Domain;

public class OrderModel
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string DeliveryRecipientName { get; set; } = string.Empty;
    public string DeliveryLine1 { get; set; } = string.Empty;
    public string? DeliveryLine2 { get; set; }
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryRegion { get; set; } = string.Empty;
    public string DeliveryPostalCode { get; set; } = string.Empty;
    public string DeliveryCountryCode { get; set; } = string.Empty;
    public string? DeliveryPhoneNumber { get; set; }
    public decimal ItemsSubtotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal CommissionTotal { get; set; }
    public string? PromoCode { get; set; }
    public string Status { get; set; } = OrderStatus.New;
    public DateTimeOffset CreatedAt { get; set; }
    public List<OrderShippingSelectionModel> ShippingSelections { get; set; } = new();
    public List<OrderItemModel> Items { get; set; } = new();
    public List<SellerOrderModel> SubOrders { get; set; } = new();
}

public class OrderItemModel
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SellerOrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string Status { get; set; } = OrderStatus.Preparing;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public SellerOrderModel? SellerOrder { get; set; }
}

public class OrderShippingSelectionModel
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int SellerOrderId { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string ShippingMethod { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string? DeliveryEstimate { get; set; }
    public DateTimeOffset? EstimatedDeliveryDate { get; set; }
    public SellerOrderModel? SellerOrder { get; set; }
}

public class SellerOrderModel
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public OrderModel? Order { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public decimal ItemsSubtotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal CommissionRateApplied { get; set; }
    public decimal CommissionAmount { get; set; }
    public DateTimeOffset? CommissionCalculatedAt { get; set; }
    public string Status { get; set; } = OrderStatus.New;
    public string? TrackingNumber { get; set; }
    public string? TrackingCarrier { get; set; }
    public string? TrackingUrl { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public OrderShippingSelectionModel? ShippingSelection { get; set; }
    public List<OrderItemModel> Items { get; set; } = new();
    public List<ReturnRequestModel> ReturnRequests { get; set; } = new();
    public List<ShippingStatusHistory> ShippingHistory { get; set; } = new();
}

public static class OrderStatus
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string New = "new";
    public const string Paid = "paid";
    public const string Preparing = "preparing";
    public const string Shipped = "shipped";
    public const string Delivered = "delivered";
    public const string Cancelled = "cancelled";
    public const string Refunded = "refunded";
    public const string Failed = "failed";
}
