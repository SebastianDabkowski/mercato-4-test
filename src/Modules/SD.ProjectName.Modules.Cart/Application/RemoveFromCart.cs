using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application
{
    public class RemoveFromCart
    {
        private readonly ICartRepository _repository;

        public RemoveFromCart(ICartRepository repository)
        {
            _repository = repository;
        }

        public async Task ExecuteAsync(int cartItemId, string buyerId)
        {
            var item = await _repository.GetByIdAsync(cartItemId);
            if (item is null || !string.Equals(item.BuyerId, buyerId, StringComparison.Ordinal))
            {
                return;
            }

            await _repository.RemoveAsync(cartItemId);
        }
    }
}
