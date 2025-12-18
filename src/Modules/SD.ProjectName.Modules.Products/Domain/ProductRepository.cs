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

        public async Task<List<ProductModel>> GetList()
        {
            return await _context.Set<ProductModel>()
                .Where(p => p.Status == ProductStatuses.Active)
                .ToListAsync();
        }

        public async Task<List<ProductModel>> GetBySeller(string sellerId, bool includeDrafts)
        {
            var query = _context.Set<ProductModel>()
                .Where(p => p.SellerId == sellerId);

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
    }
}
