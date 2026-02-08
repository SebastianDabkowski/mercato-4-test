using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class CartOperations
{
    private readonly ICartRepository _cartRepository;
    private readonly CartCalculationService _calculationService;

    public CartOperations(ICartRepository cartRepository, CartCalculationService calculationService)
    {
        _cartRepository = cartRepository;
        _calculationService = calculationService;
    }

    public async Task<CartModel> AddItemAsync(string userId, CartItemModel item)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId) ?? await _cartRepository.CreateAsync(userId);

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
        if (existingItem != null)
        {
            existingItem.Quantity += item.Quantity;
        }
        else
        {
            cart.Items.Add(item);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _cartRepository.UpdateAsync(cart);

        return cart;
    }

    public async Task<CartModel> RemoveItemAsync(string userId, int productId)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId);
        if (cart == null)
        {
            throw new InvalidOperationException("Cart not found");
        }

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            cart.Items.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await _cartRepository.UpdateAsync(cart);
        }

        return cart;
    }

    public async Task<CartModel> UpdateQuantityAsync(string userId, int productId, int quantity)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId);
        if (cart == null)
        {
            throw new InvalidOperationException("Cart not found");
        }

        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            if (quantity <= 0)
            {
                cart.Items.Remove(item);
            }
            else
            {
                item.Quantity = quantity;
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _cartRepository.UpdateAsync(cart);
        }

        return cart;
    }

    public async Task<CartTotals> GetCartTotalsAsync(string userId, decimal commissionRate = 0.01m)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId);
        if (cart == null)
        {
            return new CartTotals();
        }

        var shippingRules = await _cartRepository.GetShippingRulesAsync();
        return _calculationService.CalculateTotals(cart, shippingRules, commissionRate);
    }
}
