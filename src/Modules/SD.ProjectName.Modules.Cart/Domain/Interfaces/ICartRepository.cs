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
      
        Task<CartItemModel> AddAsync(CartItemModel item);
        Task<CartModel> CreateAsync(string userId);
        
        Task UpdateAsync(CartModel cart);
        Task UpdateAsync(CartItemModel item);
      
        Task RemoveAsync(int id);
    }
}
