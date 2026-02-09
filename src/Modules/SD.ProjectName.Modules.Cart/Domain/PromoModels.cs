namespace SD.ProjectName.Modules.Cart.Domain;

public class PromoCodeModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PromoDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public string? SellerId { get; set; }
    public decimal? MinimumSubtotal { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum PromoDiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

public class PromoSelectionModel
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string PromoCode { get; set; } = string.Empty;
    public DateTimeOffset AppliedAt { get; set; }
}

public record PromoDiscountEvaluation(bool IsEligible, decimal DiscountAmount, string? FailureReason)
{
    public static PromoDiscountEvaluation Failed(string reason) => new(false, 0, reason);
    public static PromoDiscountEvaluation Success(decimal discountAmount) => new(true, discountAmount, null);
}
