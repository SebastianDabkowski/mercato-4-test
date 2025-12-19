using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.Modules.Products.Infrastructure;

namespace SD.ProjectName.Modules.Products.Domain
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ProductDbContext _context;

        public CategoryRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<CategoryModel> Add(CategoryModel category)
        {
            _context.Set<CategoryModel>().Add(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task Delete(CategoryModel category)
        {
            _context.Set<CategoryModel>().Remove(category);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsByName(string normalizedName, int? excludeCategoryId = null)
        {
            var query = _context.Set<CategoryModel>()
                .Where(c => c.NormalizedName == normalizedName);

            if (excludeCategoryId.HasValue)
            {
                query = query.Where(c => c.Id != excludeCategoryId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<List<CategoryModel>> GetAll(bool includeInactive = true)
        {
            var query = _context.Set<CategoryModel>()
                .Include(c => c.Children)
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            return await query.ToListAsync();
        }

        public async Task<CategoryModel?> GetById(int id)
        {
            return await _context.Set<CategoryModel>()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<CategoryModel>> GetByParent(int? parentId, bool includeInactive = true)
        {
            var query = _context.Set<CategoryModel>()
                .Where(c => c.ParentId == parentId);

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            return await query.ToListAsync();
        }

        public async Task<int> GetNextDisplayOrder(int? parentId)
        {
            var maxOrder = await _context.Set<CategoryModel>()
                .Where(c => c.ParentId == parentId)
                .Select(c => (int?)c.DisplayOrder)
                .MaxAsync();

            return (maxOrder ?? -1) + 1;
        }

        public async Task<bool> HasChildren(int categoryId)
        {
            return await _context.Set<CategoryModel>()
                .AnyAsync(c => c.ParentId == categoryId);
        }

        public async Task Update(CategoryModel category)
        {
            _context.Set<CategoryModel>().Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRange(IEnumerable<CategoryModel> categories)
        {
            _context.Set<CategoryModel>().UpdateRange(categories);
            await _context.SaveChangesAsync();
        }
    }
}
