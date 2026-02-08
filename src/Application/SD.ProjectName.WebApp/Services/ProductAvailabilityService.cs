using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.WebApp.Services
{
    public class ProductAvailabilityService : IProductAvailabilityService
    {
        private readonly IProductRepository _productRepository;

        public ProductAvailabilityService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<int?> GetAvailableStockAsync(int productId)
        {
            var product = await _productRepository.GetById(productId);
            return product?.Stock;
        }
    }
}
