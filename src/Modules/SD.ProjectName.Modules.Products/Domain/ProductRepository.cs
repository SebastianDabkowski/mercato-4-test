using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.Modules.Products.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SD.ProjectName.Modules.Products.Domain
{

    public class ProductRepository : IProductRepository
    {
        private readonly ProductDbContext _context;

        public ProductRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductModel>> GetList(string? category = null)
        {
            var query = _context.Set<ProductModel>()
                .Where(p => p.Status == ProductStatuses.Active);

            var normalizedCategory = category?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedCategory))
            {
                query = query.Where(p => p.Category == normalizedCategory);
            }

            return await query.ToListAsync();
        }

        public async Task<List<ProductModel>> GetBySeller(string sellerId, bool includeDrafts)
        {
            var query = _context.Set<ProductModel>()
                .Where(p => p.SellerId == sellerId && p.Status != ProductStatuses.Archived);

            if (!includeDrafts)
            {
                query = query.Where(p => p.Status == ProductStatuses.Active);
            }

            return await query.ToListAsync();
        }

        public async Task<ProductModel> Add(ProductModel product)
        {
            _context.Set<ProductModel>().Add(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task<ProductModel?> GetById(int id)
        {
            return await _context.Set<ProductModel>()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task Update(ProductModel product)
        {
            _context.Set<ProductModel>().Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AnyWithCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return false;
            }

            var normalized = categoryName.Trim();
            return await _context.Set<ProductModel>()
                .AnyAsync(p => p.Category == normalized);
        }

        public async Task<int> UpdateCategoryName(string oldCategoryName, string newCategoryName)
        {
            if (string.IsNullOrWhiteSpace(oldCategoryName) || string.IsNullOrWhiteSpace(newCategoryName))
            {
                return 0;
            }

            var normalizedOld = oldCategoryName.Trim();
            var normalizedNew = newCategoryName.Trim();

            return await _context.Set<ProductModel>()
                .Where(p => p.Category == normalizedOld)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Category, normalizedNew));
        }
    }
}
