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
      
        Task<CartItemModel> AddAsync(CartItemModel item);
        Task<CartModel> CreateAsync(string userId);
        
        Task UpdateAsync(CartModel cart);
      
        Task RemoveAsync(int id);
    }
}
