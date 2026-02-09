using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Services;
using CartDomainModel = SD.ProjectName.Modules.Cart.Domain.CartModel;

namespace SD.ProjectName.WebApp.Pages.Buyer
{
    public class CartModel : PageModel
    {
        private readonly GetCartItems _getCartItems;
        private readonly RemoveFromCart _removeFromCart;
        private readonly UpdateCartItemQuantity _updateQuantity;
        private readonly IProductAvailabilityService _productAvailabilityService;
        private readonly ICartIdentityService _cartIdentityService;
        private readonly CartCalculationService _cartCalculationService;
        private readonly PromoService _promoService;

        public CartModel(
            GetCartItems getCartItems,
            RemoveFromCart removeFromCart,
            UpdateCartItemQuantity updateQuantity,
            IProductAvailabilityService productAvailabilityService,
            ICartIdentityService cartIdentityService,
            CartCalculationService cartCalculationService,
            PromoService promoService)
        {
            _getCartItems = getCartItems;
            _removeFromCart = removeFromCart;
            _updateQuantity = updateQuantity;
            _productAvailabilityService = productAvailabilityService;
            _cartIdentityService = cartIdentityService;
            _cartCalculationService = cartCalculationService;
            _promoService = promoService;
        }

        public List<SellerGroup> SellerGroups { get; set; } = new();
        public decimal CartTotal { get; set; }
        public CartTotals Totals { get; private set; } = new();
        public Dictionary<int, CartItemAvailability> ItemAvailability { get; private set; } = new();
        public string? PromoError { get; private set; }
        public string? PromoSuccess { get; private set; }

        public async Task OnGetAsync()
        {
            PromoError = TempData["PromoError"] as string;
            PromoSuccess = TempData["PromoSuccess"] as string;

            var buyerId = _cartIdentityService.GetOrCreateBuyerId();
            var items = await _getCartItems.ExecuteAsync(buyerId);

            ItemAvailability = await BuildAvailabilityAsync(items);
            SellerGroups = items
                .GroupBy(i => new { i.SellerId, i.SellerName })
                .Select(g => new SellerGroup(g.Key.SellerId, g.Key.SellerName, g.ToList()))
                .ToList();

            Totals = BuildTotals(items);
            var promoTotals = await _promoService.ApplyExistingAsync(buyerId, Totals);
            if (!promoTotals.HasPromo && promoTotals.ErrorMessage is not null)
            {
                PromoError ??= promoTotals.ErrorMessage;
            }

            Totals = promoTotals.Totals;
            CartTotal = Totals.TotalAmount;
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

        public async Task<IActionResult> OnPostApplyPromoAsync(string promoCode)
        {
            var buyerId = _cartIdentityService.GetOrCreateBuyerId();
            var result = await _promoService.ApplyAsync(buyerId, promoCode);
            if (result.Success)
            {
                TempData["PromoSuccess"] = $"Promo code {result.AppliedPromoCode} applied.";
            }
            else
            {
                TempData["PromoError"] = result.ErrorMessage ?? "Unable to apply promo code.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostClearPromoAsync()
        {
            var buyerId = _cartIdentityService.GetOrCreateBuyerId();
            await _promoService.ClearAsync(buyerId);
            TempData["PromoSuccess"] = "Promo code removed.";
            return RedirectToPage();
        }

        private CartTotals BuildTotals(List<CartItemModel> items)
        {
            var cart = new CartDomainModel { Items = items };
            return _cartCalculationService.CalculateTotals(cart, new List<ShippingRuleModel>());
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
