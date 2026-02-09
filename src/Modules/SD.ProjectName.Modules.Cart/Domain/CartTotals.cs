namespace SD.ProjectName.Modules.Cart.Domain;

public class CartTotals
{
    public decimal ItemsSubtotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PromoCode { get; set; }
    public List<SellerCartTotals> SellerBreakdown { get; set; } = new();
}

public class SellerCartTotals
{
    public string SellerId { get; set; } = string.Empty;
    public decimal ItemsSubtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal TotalBeforeCommission { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal SellerPayout { get; set; }
}
