using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SD.ProjectName.Modules.Products.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<List<ProductModel>> GetList();
        Task<List<ProductModel>> GetBySeller(string sellerId, bool includeDrafts);
        Task<ProductModel> Add(ProductModel product);
    }
}
