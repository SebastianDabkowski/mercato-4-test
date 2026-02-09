using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class CartMergeService
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductAvailabilityService _productAvailabilityService;

    public CartMergeService(ICartRepository cartRepository, IProductAvailabilityService productAvailabilityService)
    {
        _cartRepository = cartRepository;
        _productAvailabilityService = productAvailabilityService;
    }

    public async Task MergeAsync(string? guestBuyerId, string userBuyerId)
    {
        if (string.IsNullOrWhiteSpace(guestBuyerId) || string.IsNullOrWhiteSpace(userBuyerId) || guestBuyerId == userBuyerId)
        {
            return;
        }

        var guestItems = await _cartRepository.GetByBuyerIdAsync(guestBuyerId);
        if (guestItems.Count == 0)
        {
            return;
        }

        var userItems = await _cartRepository.GetByBuyerIdAsync(userBuyerId);

        foreach (var guestItem in guestItems)
        {
            var availableStock = await _productAvailabilityService.GetAvailableStockAsync(guestItem.ProductId);
            if (availableStock is null || availableStock <= 0)
            {
                await _cartRepository.RemoveAsync(guestItem.Id);
                continue;
            }

            var existingUserItem = userItems.FirstOrDefault(i => i.ProductId == guestItem.ProductId);
            if (existingUserItem is not null)
            {
                existingUserItem.Quantity = Math.Min(existingUserItem.Quantity + guestItem.Quantity, availableStock.Value);
                await _cartRepository.UpdateAsync(existingUserItem);
                await _cartRepository.RemoveAsync(guestItem.Id);
                continue;
            }

            guestItem.BuyerId = userBuyerId;
            guestItem.Quantity = Math.Min(guestItem.Quantity, availableStock.Value);
            await _cartRepository.UpdateAsync(guestItem);
        }
    }
}
