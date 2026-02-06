using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.Modules.Cart.Infrastructure;

namespace SD.ProjectName.Modules.Cart.Domain
{
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

        public async Task<CartItemModel> AddAsync(CartItemModel item)
        {
            _context.CartItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
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
    }
}
