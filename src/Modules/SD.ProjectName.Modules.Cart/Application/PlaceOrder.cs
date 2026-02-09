using System.Transactions;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class PlaceOrder
{
    private readonly ICheckoutValidationService _checkoutValidationService;
    private readonly ICartRepository _cartRepository;
    private readonly CartCalculationService _cartCalculationService;
    private readonly TimeProvider _timeProvider;

    public PlaceOrder(
        ICheckoutValidationService checkoutValidationService,
        ICartRepository cartRepository,
        CartCalculationService cartCalculationService,
        TimeProvider timeProvider)
    {
        _checkoutValidationService = checkoutValidationService;
        _cartRepository = cartRepository;
        _cartCalculationService = cartCalculationService;
        _timeProvider = timeProvider;
    }

    public async Task<PlaceOrderResult> ExecuteAsync(string buyerId)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var validation = await _checkoutValidationService.ValidateAsync(buyerId);
        if (!validation.IsValid)
        {
            return PlaceOrderResult.Failed(validation.Issues);
        }

        var shippingRules = await _cartRepository.GetShippingRulesAsync();
        var shippingSelectionMap = validation.ShippingSelections.ToDictionary(
            s => s.SellerId,
            s => s.ShippingMethod,
            StringComparer.OrdinalIgnoreCase);

        var cart = new CartModel { Items = validation.CartItems };
        var totals = _cartCalculationService.CalculateTotals(
            cart,
            shippingRules,
            selectedShippingMethods: shippingSelectionMap);

        var order = new OrderModel
        {
            BuyerId = buyerId,
            PaymentMethod = validation.PaymentSelection?.PaymentMethod ?? string.Empty,
            ItemsSubtotal = totals.ItemsSubtotal,
            ShippingTotal = totals.ShippingTotal,
            TotalAmount = totals.TotalAmount,
            CreatedAt = _timeProvider.GetUtcNow(),
            Status = OrderStatus.Pending,
            Items = validation.CartItems.Select(item => new OrderItemModel
            {
                ProductId = item.ProductId,
                ProductSku = item.ProductSku,
                ProductName = item.ProductName,
                SellerId = item.SellerId,
                SellerName = item.SellerName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity
            }).ToList()
        };

        await _cartRepository.AddOrderAsync(order);

        scope.Complete();
        return PlaceOrderResult.Completed(order, validation.PaymentSelection, validation.ShippingSelections, validation.DeliveryAddress);
    }
}

public record PlaceOrderResult(
    bool Success,
    OrderModel? Order,
    PaymentSelectionModel? PaymentSelection,
    List<ShippingSelectionModel> ShippingSelections,
    DeliveryAddressModel? DeliveryAddress,
    List<CheckoutValidationIssue> Issues)
{
    public static PlaceOrderResult Failed(List<CheckoutValidationIssue> issues) =>
        new(false, null, null, new List<ShippingSelectionModel>(), null, issues);

    public static PlaceOrderResult Completed(
        OrderModel order,
        PaymentSelectionModel? paymentSelection,
        List<ShippingSelectionModel> shippingSelections,
        DeliveryAddressModel? deliveryAddress) =>
        new(true, order, paymentSelection, shippingSelections, deliveryAddress, new List<CheckoutValidationIssue>());
}
