using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application
{
    public class UpdateCartItemQuantity
    {
        private readonly ICartRepository _repository;
        private readonly IProductAvailabilityService _productAvailabilityService;

        public UpdateCartItemQuantity(ICartRepository repository, IProductAvailabilityService productAvailabilityService)
        {
            _repository = repository;
            _productAvailabilityService = productAvailabilityService;
        }

        public async Task<bool> ExecuteAsync(int cartItemId, int newQuantity)
        {
            var item = await _repository.GetByIdAsync(cartItemId);
            if (item is null)
            {
                return false;
            }

            if (newQuantity <= 0)
            {
                await _repository.RemoveAsync(cartItemId);
                return true;
            }

            var availableStock = await _productAvailabilityService.GetAvailableStockAsync(item.ProductId);
            if (availableStock is null || availableStock <= 0)
            {
                await _repository.RemoveAsync(cartItemId);
                return true;
            }

            var clampedQuantity = Math.Min(newQuantity, availableStock.Value);

            item.Quantity = clampedQuantity;
            await _repository.UpdateAsync(item);
            return true;
        }
    }
}
