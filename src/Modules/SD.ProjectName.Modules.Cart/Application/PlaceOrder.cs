using System.Linq;
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

        var itemsBySeller = validation.CartItems
            .GroupBy(i => i.SellerId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SellerName, StringComparer.OrdinalIgnoreCase);

        var orderShippingSelections = validation.ShippingSelections.Select(selection => new OrderShippingSelectionModel
        {
            SellerId = selection.SellerId,
            SellerName = itemsBySeller.GetValueOrDefault(selection.SellerId, string.Empty),
            ShippingMethod = selection.ShippingMethod,
            Cost = selection.Cost
        }).ToList();

        var order = new OrderModel
        {
            BuyerId = buyerId,
            PaymentMethod = validation.PaymentSelection?.PaymentMethod ?? string.Empty,
            DeliveryRecipientName = validation.DeliveryAddress?.RecipientName ?? string.Empty,
            DeliveryLine1 = validation.DeliveryAddress?.Line1 ?? string.Empty,
            DeliveryLine2 = validation.DeliveryAddress?.Line2,
            DeliveryCity = validation.DeliveryAddress?.City ?? string.Empty,
            DeliveryRegion = validation.DeliveryAddress?.Region ?? string.Empty,
            DeliveryPostalCode = validation.DeliveryAddress?.PostalCode ?? string.Empty,
            DeliveryCountryCode = validation.DeliveryAddress?.CountryCode ?? string.Empty,
            DeliveryPhoneNumber = validation.DeliveryAddress?.PhoneNumber,
            ItemsSubtotal = totals.ItemsSubtotal,
            ShippingTotal = totals.ShippingTotal,
            TotalAmount = totals.TotalAmount,
            CreatedAt = _timeProvider.GetUtcNow(),
            Status = OrderStatus.Confirmed,
            Items = validation.CartItems.Select(item => new OrderItemModel
            {
                ProductId = item.ProductId,
                ProductSku = item.ProductSku,
                ProductName = item.ProductName,
                SellerId = item.SellerId,
                SellerName = item.SellerName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity
            }).ToList(),
            ShippingSelections = orderShippingSelections
        };

        await _cartRepository.AddOrderAsync(order);
        await _cartRepository.ClearCartItemsAsync(buyerId);
        await _cartRepository.ClearShippingSelectionsAsync(buyerId);
        await _cartRepository.ClearPaymentSelectionAsync(buyerId);

        scope.Complete();
        return PlaceOrderResult.Completed(order, validation.PaymentSelection, orderShippingSelections, validation.DeliveryAddress);
    }
}

public record PlaceOrderResult(
    bool Success,
    OrderModel? Order,
    PaymentSelectionModel? PaymentSelection,
    List<OrderShippingSelectionModel> ShippingSelections,
    DeliveryAddressModel? DeliveryAddress,
    List<CheckoutValidationIssue> Issues)
{
    public static PlaceOrderResult Failed(List<CheckoutValidationIssue> issues) =>
        new(false, null, null, new List<OrderShippingSelectionModel>(), null, issues);

    public static PlaceOrderResult Completed(
        OrderModel order,
        PaymentSelectionModel? paymentSelection,
        List<OrderShippingSelectionModel> shippingSelections,
        DeliveryAddressModel? deliveryAddress) =>
        new(true, order, paymentSelection, shippingSelections, deliveryAddress, new List<CheckoutValidationIssue>());
}
