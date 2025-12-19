using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Products.Domain.Interfaces;

namespace SD.ProjectName.Modules.Products.Application
{
    public class DeleteProduct
    {
        private readonly IProductRepository _repository;

        public DeleteProduct(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> ArchiveAsync(int productId, string sellerId)
        {
            var existing = await _repository.GetById(productId);
            if (existing is null || !string.Equals(existing.SellerId, sellerId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            existing.Status = ProductStatuses.Archived;
            await _repository.Update(existing);
            return true;
        }
    }
}
