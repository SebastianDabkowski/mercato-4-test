using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application
{
    public class UpdateCartItemQuantity
    {
        private readonly ICartRepository _repository;

        public UpdateCartItemQuantity(ICartRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> ExecuteAsync(int cartItemId, int newQuantity)
        {
            if (newQuantity < 1)
            {
                return false;
            }

            var item = await _repository.GetByIdAsync(cartItemId);
            if (item is null)
            {
                return false;
            }

            item.Quantity = newQuantity;
            await _repository.UpdateAsync(item);
            return true;
        }
    }
}
