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
    private readonly PromoService _promoService;
    private readonly TimeProvider _timeProvider;

    public PlaceOrder(
        ICheckoutValidationService checkoutValidationService,
        ICartRepository cartRepository,
        CartCalculationService cartCalculationService,
        PromoService promoService,
        TimeProvider timeProvider)
    {
        _checkoutValidationService = checkoutValidationService;
        _cartRepository = cartRepository;
        _cartCalculationService = cartCalculationService;
        _promoService = promoService;
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

        var promoTotals = await _promoService.ApplyExistingAsync(buyerId, totals);
        if (promoTotals.ErrorMessage is not null && promoTotals.HasPromo)
        {
            return PlaceOrderResult.Failed(new List<CheckoutValidationIssue>
            {
                CheckoutValidationIssue.ForCart("promo-invalid", promoTotals.ErrorMessage)
            });
        }

        totals = promoTotals.Totals;

        var sellerGroups = validation.CartItems
            .GroupBy(i => i.SellerId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                SellerId = g.Key,
                SellerName = g.First().SellerName,
                Items = g.ToList()
            })
            .ToList();

        var selectionBySeller = validation.ShippingSelections
            .ToDictionary(s => s.SellerId, s => s, StringComparer.OrdinalIgnoreCase);

        var orderShippingSelections = validation.ShippingSelections.Select(selection => new OrderShippingSelectionModel
        {
            SellerId = selection.SellerId,
            SellerName = sellerGroups.FirstOrDefault(g => g.SellerId.Equals(selection.SellerId, StringComparison.OrdinalIgnoreCase))?.SellerName ?? string.Empty,
            ShippingMethod = selection.ShippingMethod,
            Cost = selection.Cost
        }).ToList();

        var totalItemsSubtotal = sellerGroups.Sum(g => g.Items.Sum(i => i.UnitPrice * i.Quantity));
        decimal remainingDiscount = totals.DiscountTotal;
        var subOrders = new List<SellerOrderModel>();
        var orderItems = new List<OrderItemModel>();

        for (var index = 0; index < sellerGroups.Count; index++)
        {
            var group = sellerGroups[index];
            var itemsSubtotal = group.Items.Sum(i => i.UnitPrice * i.Quantity);
            var selection = selectionBySeller.GetValueOrDefault(group.SellerId);
            var shippingCost = selection?.Cost ?? 0m;

            var discountShare = 0m;
            if (totals.DiscountTotal > 0 && totalItemsSubtotal > 0)
            {
                var proportional = totals.DiscountTotal * (itemsSubtotal / totalItemsSubtotal);
                discountShare = Math.Round(proportional, 2, MidpointRounding.AwayFromZero);
                var isLastSeller = index == sellerGroups.Count - 1;
                if (isLastSeller)
                {
                    discountShare = remainingDiscount;
                }

                remainingDiscount -= discountShare;
            }

            var sellerOrder = new SellerOrderModel
            {
                SellerId = group.SellerId,
                SellerName = group.SellerName,
                ItemsSubtotal = itemsSubtotal,
                ShippingTotal = shippingCost,
                DiscountTotal = discountShare,
                TotalAmount = Math.Max(0m, itemsSubtotal + shippingCost - discountShare),
                Status = OrderStatus.Paid
            };

            var selectionModel = orderShippingSelections.FirstOrDefault(s =>
                s.SellerId.Equals(group.SellerId, StringComparison.OrdinalIgnoreCase));
            if (selectionModel is null && selection is not null)
            {
                selectionModel = new OrderShippingSelectionModel
                {
                    SellerId = selection.SellerId,
                    SellerName = group.SellerName,
                    ShippingMethod = selection.ShippingMethod,
                    Cost = selection.Cost
                };
                orderShippingSelections.Add(selectionModel);
            }

            var sellerOrderItems = new List<OrderItemModel>();
            foreach (var item in group.Items)
            {
                var orderItem = new OrderItemModel
                {
                    ProductId = item.ProductId,
                    ProductSku = item.ProductSku,
                    ProductName = item.ProductName,
                    SellerId = item.SellerId,
                    SellerName = item.SellerName,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    SellerOrder = sellerOrder
                };

                orderItems.Add(orderItem);
                sellerOrderItems.Add(orderItem);
            }

            if (selectionModel is not null)
            {
                selectionModel.SellerOrder = sellerOrder;
                sellerOrder.ShippingSelection = selectionModel;
            }

            sellerOrder.Items = sellerOrderItems;
            subOrders.Add(sellerOrder);
        }

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
            DiscountTotal = totals.DiscountTotal,
            TotalAmount = totals.TotalAmount,
            PromoCode = promoTotals.AppliedPromoCode,
            CreatedAt = _timeProvider.GetUtcNow(),
            Status = OrderStatus.Paid,
            Items = orderItems,
            ShippingSelections = orderShippingSelections,
            SubOrders = subOrders
        };

        await _cartRepository.AddOrderAsync(order);
        await _cartRepository.ClearCartItemsAsync(buyerId);
        await _cartRepository.ClearShippingSelectionsAsync(buyerId);
        await _cartRepository.ClearPaymentSelectionAsync(buyerId);
        await _cartRepository.ClearPromoSelectionAsync(buyerId);

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
