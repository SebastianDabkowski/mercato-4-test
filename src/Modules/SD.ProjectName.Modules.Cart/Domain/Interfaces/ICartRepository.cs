using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Modules.Cart.Domain.Interfaces
{
    public interface ICartRepository
    {
        Task<List<CartItemModel>> GetByBuyerIdAsync(string buyerId);
        Task<CartItemModel?> GetByBuyerAndProductAsync(string buyerId, int productId);
        Task<CartItemModel?> GetByIdAsync(int id);
        Task<CartModel?> GetByUserIdAsync(string userId);
        Task<List<ShippingRuleModel>> GetShippingRulesAsync();
        Task<List<DeliveryAddressModel>> GetAddressesAsync(string buyerId);
        Task<DeliveryAddressModel?> GetAddressAsync(int addressId);
        Task<DeliveryAddressModel> AddOrUpdateAddressAsync(DeliveryAddressModel address);
        Task SetSelectedAddressAsync(string buyerId, int addressId);
        Task ClearSelectedAddressAsync(string buyerId);
        Task<DeliveryAddressModel?> GetSelectedAddressAsync(string buyerId);
        Task<List<ShippingSelectionModel>> GetShippingSelectionsAsync(string buyerId);
        Task SetShippingSelectionAsync(string buyerId, string sellerId, string shippingMethod, decimal cost);
        Task ClearShippingSelectionsAsync(string buyerId);
        Task<PaymentSelectionModel?> GetPaymentSelectionAsync(string buyerId);
        Task<PaymentSelectionModel> UpsertPaymentSelectionAsync(PaymentSelectionModel selection);
        Task ClearPaymentSelectionAsync(string buyerId);
        Task<PromoSelectionModel?> GetPromoSelectionAsync(string buyerId);
        Task<PromoSelectionModel> UpsertPromoSelectionAsync(PromoSelectionModel selection);
        Task ClearPromoSelectionAsync(string buyerId);
        Task<PromoCodeModel?> GetPromoCodeAsync(string code);
      
        Task<CartItemModel> AddAsync(CartItemModel item);
        Task<CartModel> CreateAsync(string userId);
        Task<OrderModel> AddOrderAsync(OrderModel order);
        Task<ReturnRequestModel> AddReturnRequestAsync(ReturnRequestModel request);
        Task<OrderModel?> GetOrderAsync(int orderId, string buyerId);
        Task<OrderModel?> GetOrderWithSubOrdersAsync(int orderId);
        Task<List<OrderModel>> GetOrdersForBuyerAsync(string buyerId);
        Task<BuyerOrdersResult> GetOrdersForBuyerAsync(string buyerId, BuyerOrdersQuery query);
        Task<SellerOrderModel?> GetSellerOrderAsync(int sellerOrderId, string sellerId);
        Task<SellerOrderModel?> GetSellerOrderByIdAsync(int sellerOrderId);
        Task<SellerOrdersResult> GetSellerOrdersAsync(string sellerId, SellerOrdersQuery query);
        Task SaveChangesAsync();
        
        Task UpdateAsync(CartModel cart);
        Task UpdateAsync(CartItemModel item);
        Task ClearCartItemsAsync(string buyerId);
      
        Task RemoveAsync(int id);
    }
}
