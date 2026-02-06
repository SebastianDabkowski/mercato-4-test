using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application
{
    public class GetCartItems
    {
        private readonly ICartRepository _repository;

        public GetCartItems(ICartRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<CartItemModel>> ExecuteAsync(string buyerId)
        {
            return await _repository.GetByBuyerIdAsync(buyerId);
        }
    }
}
