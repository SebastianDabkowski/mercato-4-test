using SD.ProjectName.Modules.Cart.Domain;

namespace SD.ProjectName.Modules.Cart.Domain.Interfaces;

public interface ICartRepository
{
    Task<CartModel?> GetByUserIdAsync(string userId);
    Task<CartModel> CreateAsync(string userId);
    Task UpdateAsync(CartModel cart);
    Task<List<ShippingRuleModel>> GetShippingRulesAsync();
}
