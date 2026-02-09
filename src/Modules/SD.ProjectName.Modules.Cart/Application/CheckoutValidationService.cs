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

    public CheckoutValidationService(
        GetCartItems getCartItems,
        ICartRepository cartRepository,
        IProductSnapshotService productSnapshotService)
    {
        _getCartItems = getCartItems;
        _cartRepository = cartRepository;
        _productSnapshotService = productSnapshotService;
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

        var shippingSelections = await _cartRepository.GetShippingSelectionsAsync(buyerId);
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
