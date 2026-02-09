using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public interface ICheckoutValidationService
{
    Task<CheckoutValidationResult> ValidateAsync(string buyerId, bool requirePaymentAuthorized = true);
}

public class CheckoutValidationService : ICheckoutValidationService
{
    private readonly GetCartItems _getCartItems;
    private readonly ICartRepository _cartRepository;
    private readonly IProductSnapshotService _productSnapshotService;
    private readonly CartCalculationService _cartCalculationService;

    public CheckoutValidationService(
        GetCartItems getCartItems,
        ICartRepository cartRepository,
        IProductSnapshotService productSnapshotService,
        CartCalculationService cartCalculationService)
    {
        _getCartItems = getCartItems;
        _cartRepository = cartRepository;
        _productSnapshotService = productSnapshotService;
        _cartCalculationService = cartCalculationService;
    }

    public async Task<CheckoutValidationResult> ValidateAsync(string buyerId, bool requirePaymentAuthorized = true)
    {
        var issues = new List<CheckoutValidationIssue>();
        var cartItems = await _getCartItems.ExecuteAsync(buyerId);
        if (!cartItems.Any())
        {
            issues.Add(CheckoutValidationIssue.ForCart("cart-empty", "Your cart is empty."));
            return BuildResult(issues, cartItems, null, null, new List<ShippingSelectionModel>());
        }

        var paymentSelection = await _cartRepository.GetPaymentSelectionAsync(buyerId);
        if (paymentSelection is null)
        {
            issues.Add(CheckoutValidationIssue.ForCart("payment-required", "Payment authorization is required before placing the order."));
        }
        else if (requirePaymentAuthorized &&
                 paymentSelection.Status != PaymentStatus.Authorized &&
                 paymentSelection.Status != PaymentStatus.Paid)
        {
            issues.Add(CheckoutValidationIssue.ForCart("payment-required", "Payment authorization is required before placing the order."));
        }

        var selectedAddress = await _cartRepository.GetSelectedAddressAsync(buyerId);
        if (selectedAddress is null)
        {
            issues.Add(CheckoutValidationIssue.ForCart("address-required", "Select a delivery address before placing the order."));
        }

        var shippingRules = await _cartRepository.GetShippingRulesAsync() ?? new List<ShippingRuleModel>();
        var shippingSelections = await _cartRepository.GetShippingSelectionsAsync(buyerId);
        shippingSelections = NormalizeShippingSelections(
            cartItems,
            shippingSelections,
            shippingRules,
            selectedAddress,
            issues);
        if (!HasSelectionsForAllSellers(cartItems, shippingSelections))
        {
            issues.Add(CheckoutValidationIssue.ForCart("shipping-required", "Choose shipping methods for all sellers before placing the order."));
        }

        foreach (var item in cartItems)
        {
            var snapshot = await _productSnapshotService.GetSnapshotAsync(item.ProductId);
            if (snapshot is null || snapshot.Stock <= 0)
            {
                issues.Add(CheckoutValidationIssue.ForItem(item.ProductId, "out-of-stock", $"{item.ProductName} is no longer available.", availableStock: snapshot?.Stock ?? 0));
                continue;
            }

            if (snapshot.Stock < item.Quantity)
            {
                issues.Add(CheckoutValidationIssue.ForItem(item.ProductId, "insufficient-stock", $"{item.ProductName} has only {snapshot.Stock} left.", availableStock: snapshot.Stock));
            }

            if (snapshot.Price != item.UnitPrice)
            {
                issues.Add(CheckoutValidationIssue.ForItem(
                    item.ProductId,
                    "price-changed",
                    $"{item.ProductName} price updated to {snapshot.Price:C}.",
                    currentPrice: snapshot.Price));
            }
        }

        return BuildResult(issues, cartItems, paymentSelection, selectedAddress, shippingSelections);
    }

    private CheckoutValidationResult BuildResult(
        List<CheckoutValidationIssue> issues,
        List<CartItemModel> cartItems,
        PaymentSelectionModel? paymentSelection,
        DeliveryAddressModel? selectedAddress,
        List<ShippingSelectionModel> shippingSelections)
    {
        return new CheckoutValidationResult(
            !issues.Any(),
            issues,
            cartItems,
            shippingSelections,
            paymentSelection,
            selectedAddress);
    }

    private List<ShippingSelectionModel> NormalizeShippingSelections(
        List<CartItemModel> cartItems,
        List<ShippingSelectionModel> existingSelections,
        List<ShippingRuleModel> shippingRules,
        DeliveryAddressModel? selectedAddress,
        List<CheckoutValidationIssue> issues)
    {
        var normalized = new List<ShippingSelectionModel>();
        var groups = cartItems
            .GroupBy(i => i.SellerId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var sellerId = group.Key;
            var sellerName = group.First().SellerName;
            var sellerLabel = string.IsNullOrWhiteSpace(sellerName) ? sellerId : sellerName;
            var subtotal = group.Sum(i => i.UnitPrice * i.Quantity);
            var totalWeight = group.Sum(i => i.WeightKg * i.Quantity);

            var availableRules = shippingRules
                .Where(r => string.Equals(r.SellerId, sellerId, StringComparison.OrdinalIgnoreCase) && r.IsActive)
                .Where(r => !r.MaxWeightKg.HasValue || totalWeight <= r.MaxWeightKg.Value)
                .Where(r => IsRuleAllowedForAddress(r, selectedAddress))
                .ToList();

            if (availableRules.Count == 0)
            {
                issues.Add(CheckoutValidationIssue.ForCart("shipping-unavailable", $"Shipping is not available for {sellerLabel} to the selected address."));
                continue;
            }

            var selected = existingSelections.FirstOrDefault(s => string.Equals(s.SellerId, sellerId, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                issues.Add(CheckoutValidationIssue.ForCart("shipping-required", $"Choose a shipping method for {sellerLabel}."));
                continue;
            }

            var matchedRule = availableRules.FirstOrDefault(r =>
                string.Equals(r.ShippingMethod, selected.ShippingMethod, StringComparison.OrdinalIgnoreCase));

            if (matchedRule is null)
            {
                issues.Add(CheckoutValidationIssue.ForCart("shipping-required", $"Selected shipping is not available for {sellerLabel}."));
                continue;
            }

            var cost = _cartCalculationService.CalculateShippingCost(subtotal, totalWeight, matchedRule);
            selected.ShippingMethod = matchedRule.ShippingMethod;
            selected.Cost = cost;
            selected.DeliveryEstimate = matchedRule.DeliveryEstimate;
            normalized.Add(selected);
        }

        return normalized;
    }

    private static bool IsRuleAllowedForAddress(ShippingRuleModel rule, DeliveryAddressModel? address)
    {
        if (address is null)
        {
            return true;
        }

        var allowedCountries = SplitCsv(rule.AllowedCountryCodes);
        if (allowedCountries.Any() && !allowedCountries.Contains(address.CountryCode, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var allowedRegions = SplitCsv(rule.AllowedRegions);
        if (allowedRegions.Any() && !allowedRegions.Contains(address.Region, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static List<string> SplitCsv(string? value) =>
        value?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList()
        ?? new List<string>();

    private static bool HasSelectionsForAllSellers(IEnumerable<CartItemModel> cartItems, IEnumerable<ShippingSelectionModel> selections)
    {
        var sellerIds = cartItems.Select(i => i.SellerId).Distinct().ToList();
        return sellerIds.All(id => selections.Any(s => s.SellerId == id));
    }
}

public record CheckoutValidationIssue(
    int? ProductId,
    string Code,
    string Message,
    decimal? CurrentPrice = null,
    int? AvailableStock = null)
{
    public static CheckoutValidationIssue ForCart(string code, string message) =>
        new(null, code, message);

    public static CheckoutValidationIssue ForItem(int productId, string code, string message, decimal? currentPrice = null, int? availableStock = null) =>
        new(productId, code, message, currentPrice, availableStock);
}

public record CheckoutValidationResult(
    bool IsValid,
    List<CheckoutValidationIssue> Issues,
    List<CartItemModel> CartItems,
    List<ShippingSelectionModel> ShippingSelections,
    PaymentSelectionModel? PaymentSelection,
    DeliveryAddressModel? DeliveryAddress);
