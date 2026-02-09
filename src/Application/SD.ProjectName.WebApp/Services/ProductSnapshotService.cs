using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.WebApp.Services;

public class ProductSnapshotService : IProductSnapshotService
{
    private readonly IProductRepository _productRepository;

    public ProductSnapshotService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductSnapshot?> GetSnapshotAsync(int productId)
    {
        var product = await _productRepository.GetById(productId);
        if (product is null)
        {
            return null;
        }

        return new ProductSnapshot(product.Id, product.Price, product.Stock);
    }
}
