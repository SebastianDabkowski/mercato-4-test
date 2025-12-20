using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SD.ProjectName.Modules.Products.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<List<ProductModel>> GetList(string? category = null);
        Task<List<ProductModel>> Search(string keyword);
        Task<List<ProductModel>> GetBySeller(string sellerId, bool includeDrafts);
        Task<ProductModel?> GetById(int id);
        Task<List<ProductModel>> GetBySellerAndSkus(string sellerId, IEnumerable<string> skus);
        Task Update(ProductModel product);
        Task UpdateRange(IEnumerable<ProductModel> products);
        Task<ProductModel> Add(ProductModel product);
        Task AddRange(IEnumerable<ProductModel> products);
        Task<bool> AnyWithCategory(string categoryName);
        Task<int> UpdateCategoryName(string oldCategoryName, string newCategoryName);
    }
}
