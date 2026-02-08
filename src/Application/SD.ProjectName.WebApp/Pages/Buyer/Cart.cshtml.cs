using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.WebApp.Pages.Buyer
{
    public class CartModel : PageModel
    {
        private readonly GetCartItems _getCartItems;
        private readonly RemoveFromCart _removeFromCart;
        private readonly UpdateCartItemQuantity _updateQuantity;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProductAvailabilityService _productAvailabilityService;

        public CartModel(
            GetCartItems getCartItems,
            RemoveFromCart removeFromCart,
            UpdateCartItemQuantity updateQuantity,
            UserManager<ApplicationUser> userManager,
            IProductAvailabilityService productAvailabilityService)
        {
            _getCartItems = getCartItems;
            _removeFromCart = removeFromCart;
            _updateQuantity = updateQuantity;
            _userManager = userManager;
            _productAvailabilityService = productAvailabilityService;
        }

        public List<SellerGroup> SellerGroups { get; set; } = new();
        public decimal CartTotal { get; set; }
        public Dictionary<int, CartItemAvailability> ItemAvailability { get; private set; } = new();

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            var items = await _getCartItems.ExecuteAsync(userId);

            ItemAvailability = await BuildAvailabilityAsync(items);
            SellerGroups = items
                .GroupBy(i => new { i.SellerId, i.SellerName })
                .Select(g => new SellerGroup(g.Key.SellerId, g.Key.SellerName, g.ToList()))
                .ToList();
            CartTotal = items.Sum(i => i.UnitPrice * GetDisplayQuantity(i.Id, i.Quantity));
        }

        public async Task<IActionResult> OnPostRemoveAsync(int itemId)
        {
            await _removeFromCart.ExecuteAsync(itemId);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateQuantityAsync(int itemId, int quantity)
        {
            await _updateQuantity.ExecuteAsync(itemId, quantity);
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
