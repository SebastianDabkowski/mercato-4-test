using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application
{
    public class AddToCart
    {
        private readonly ICartRepository _repository;

        public AddToCart(ICartRepository repository)
        {
            _repository = repository;
        }

        public async Task<CartItemModel> ExecuteAsync(
            string buyerId,
            int productId,
            string productName,
            string category,
            decimal unitPrice,
            string sellerId,
            string sellerName,
            int quantity = 1)
        {
            if (quantity < 1)
            {
                throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));
            }

            var existing = await _repository.GetByBuyerAndProductAsync(buyerId, productId);
            if (existing is not null)
            {
                existing.Quantity += quantity;
                await _repository.UpdateAsync(existing);
                return existing;
            }

            var item = new CartItemModel
            {
                BuyerId = buyerId,
                ProductId = productId,
                ProductName = productName,
                Category = category,
                UnitPrice = unitPrice,
                Quantity = quantity,
                SellerId = sellerId,
                SellerName = sellerName,
                AddedAt = DateTimeOffset.UtcNow
            };

            return await _repository.AddAsync(item);
        }
    }
}
