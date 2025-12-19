namespace SD.ProjectName.Modules.Products.Domain.Interfaces
{
    public interface ICategoryRepository
    {
        Task<List<CategoryModel>> GetAll(bool includeInactive = true);
        Task<List<CategoryModel>> GetByParent(int? parentId, bool includeInactive = true);
        Task<CategoryModel?> GetById(int id);
        Task<CategoryModel> Add(CategoryModel category);
        Task Update(CategoryModel category);
        Task UpdateRange(IEnumerable<CategoryModel> categories);
        Task Delete(CategoryModel category);
        Task<bool> ExistsByName(string normalizedName, int? excludeCategoryId = null);
        Task<int> GetNextDisplayOrder(int? parentId);
        Task<bool> HasChildren(int categoryId);
    }
}
