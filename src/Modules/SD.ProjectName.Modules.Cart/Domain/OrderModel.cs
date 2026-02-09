namespace SD.ProjectName.Modules.Cart.Domain;

public class OrderModel
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal ItemsSubtotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = OrderStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public List<OrderItemModel> Items { get; set; } = new();
}

public class OrderItemModel
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public static class OrderStatus
{
    public const string Pending = "pending";
}
