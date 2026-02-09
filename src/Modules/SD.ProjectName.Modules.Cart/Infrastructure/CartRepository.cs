using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Infrastructure;

public class CartRepository : ICartRepository
{
    private readonly CartDbContext _context;

    public CartRepository(CartDbContext context)
    {
        _context = context;
    }

    public async Task<List<CartItemModel>> GetByBuyerIdAsync(string buyerId)
    {
        return await _context.CartItems
            .Where(c => c.BuyerId == buyerId)
            .OrderBy(c => c.AddedAt)
            .ToListAsync();
    }

    public async Task<CartItemModel?> GetByBuyerAndProductAsync(string buyerId, int productId)
    {
        return await _context.CartItems
            .FirstOrDefaultAsync(c => c.BuyerId == buyerId && c.ProductId == productId);
    }

    public async Task<CartItemModel?> GetByIdAsync(int id)
    {
        return await _context.CartItems.FindAsync(id);
    }

    public async Task<CartModel?> GetByUserIdAsync(string userId)
    {
        return await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<CartModel> CreateAsync(string userId)
    {
        var cart = new CartModel
        {
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Carts.Add(cart);
        await _context.SaveChangesAsync();

        return cart;
    }

    public async Task<CartItemModel> AddAsync(CartItemModel item)
    {
        _context.CartItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(CartModel cart)
    {
        _context.Carts.Update(cart);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CartItemModel item)
    {
        _context.CartItems.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(int id)
    {
        var item = await _context.CartItems.FindAsync(id);
        if (item is not null)
        {
            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ShippingRuleModel>> GetShippingRulesAsync()
    {
        return await _context.ShippingRules
            .Where(sr => sr.IsActive)
            .ToListAsync();
    }

    public async Task<List<DeliveryAddressModel>> GetAddressesAsync(string buyerId)
    {
        return await _context.DeliveryAddresses
            .Where(a => a.BuyerId == buyerId)
            .OrderByDescending(a => a.IsSelectedForCheckout)
            .ThenByDescending(a => a.UpdatedAt)
            .ToListAsync();
    }

    public async Task<DeliveryAddressModel?> GetAddressAsync(int addressId)
    {
        return await _context.DeliveryAddresses.FindAsync(addressId);
    }

    public async Task<DeliveryAddressModel> AddOrUpdateAddressAsync(DeliveryAddressModel address)
    {
        if (address.Id == 0)
        {
            _context.DeliveryAddresses.Add(address);
        }
        else
        {
            _context.DeliveryAddresses.Update(address);
        }

        await _context.SaveChangesAsync();
        return address;
    }

    public async Task SetSelectedAddressAsync(string buyerId, int addressId)
    {
        await ClearSelectedAddressAsync(buyerId);

        var existing = await _context.DeliveryAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.BuyerId == buyerId);
        if (existing is null)
        {
            return;
        }

        existing.IsSelectedForCheckout = true;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ClearSelectedAddressAsync(string buyerId)
    {
        var selected = await _context.DeliveryAddresses
            .Where(a => a.BuyerId == buyerId && a.IsSelectedForCheckout)
            .ToListAsync();

        if (selected.Count == 0)
        {
            return;
        }

        foreach (var address in selected)
        {
            address.IsSelectedForCheckout = false;
            address.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<DeliveryAddressModel?> GetSelectedAddressAsync(string buyerId)
    {
        return await _context.DeliveryAddresses
            .Where(a => a.BuyerId == buyerId && a.IsSelectedForCheckout)
            .OrderByDescending(a => a.UpdatedAt)
            .FirstOrDefaultAsync();
    }
}
