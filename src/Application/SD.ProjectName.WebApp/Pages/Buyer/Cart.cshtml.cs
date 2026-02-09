using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Buyer
{
    public class CartModel : PageModel
    {
        private readonly GetCartItems _getCartItems;
        private readonly RemoveFromCart _removeFromCart;
        private readonly UpdateCartItemQuantity _updateQuantity;
        private readonly IProductAvailabilityService _productAvailabilityService;
        private readonly ICartIdentityService _cartIdentityService;

        public CartModel(
            GetCartItems getCartItems,
            RemoveFromCart removeFromCart,
            UpdateCartItemQuantity updateQuantity,
            IProductAvailabilityService productAvailabilityService,
            ICartIdentityService cartIdentityService)
        {
            _getCartItems = getCartItems;
            _removeFromCart = removeFromCart;
            _updateQuantity = updateQuantity;
            _productAvailabilityService = productAvailabilityService;
            _cartIdentityService = cartIdentityService;
        }

        public List<SellerGroup> SellerGroups { get; set; } = new();
        public decimal CartTotal { get; set; }
        public Dictionary<int, CartItemAvailability> ItemAvailability { get; private set; } = new();

        public async Task OnGetAsync()
        {
            var buyerId = _cartIdentityService.GetOrCreateBuyerId();
            var items = await _getCartItems.ExecuteAsync(buyerId);

            ItemAvailability = await BuildAvailabilityAsync(items);
            SellerGroups = items
                .GroupBy(i => new { i.SellerId, i.SellerName })
                .Select(g => new SellerGroup(g.Key.SellerId, g.Key.SellerName, g.ToList()))
                .ToList();
            CartTotal = items.Sum(i => i.UnitPrice * GetDisplayQuantity(i.Id, i.Quantity));
        }

        public async Task<IActionResult> OnPostRemoveAsync(int itemId)
        {
            var buyerId = _cartIdentityService.GetOrCreateBuyerId();
            await _removeFromCart.ExecuteAsync(itemId, buyerId);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateQuantityAsync(int itemId, int quantity)
        {
            var buyerId = _cartIdentityService.GetOrCreateBuyerId();
            await _updateQuantity.ExecuteAsync(itemId, quantity, buyerId);
            return RedirectToPage();
        }

        private async Task<Dictionary<int, CartItemAvailability>> BuildAvailabilityAsync(List<CartItemModel> items)
        {
            var result = new Dictionary<int, CartItemAvailability>();

            foreach (var item in items)
            {
                var availableStock = await _productAvailabilityService.GetAvailableStockAsync(item.ProductId) ?? 0;
                var displayQuantity = availableStock > 0 ? Math.Min(item.Quantity, availableStock) : 0;
                result[item.Id] = new CartItemAvailability(availableStock, displayQuantity);
            }

            return result;
        }

        public int GetAvailableStock(int itemId)
        {
            if (ItemAvailability.TryGetValue(itemId, out var availability))
            {
                return availability.AvailableStock;
            }

            return 0;
        }

        public int GetDisplayQuantity(int itemId, int fallbackQuantity)
        {
            if (ItemAvailability.TryGetValue(itemId, out var availability))
            {
                return availability.DisplayQuantity;
            }

            return fallbackQuantity;
        }

        public record CartItemAvailability(int AvailableStock, int DisplayQuantity);

        public record SellerGroup(string SellerId, string SellerName, List<CartItemModel> Items);
    }
}
