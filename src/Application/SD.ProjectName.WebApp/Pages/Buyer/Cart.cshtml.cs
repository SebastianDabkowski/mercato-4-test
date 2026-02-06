using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.WebApp.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SD.ProjectName.WebApp.Pages.Buyer
{
    public class CartModel : PageModel
    {
        private readonly GetCartItems _getCartItems;
        private readonly RemoveFromCart _removeFromCart;
        private readonly UpdateCartItemQuantity _updateQuantity;
        private readonly UserManager<ApplicationUser> _userManager;

        public CartModel(GetCartItems getCartItems, RemoveFromCart removeFromCart, UpdateCartItemQuantity updateQuantity, UserManager<ApplicationUser> userManager)
        {
            _getCartItems = getCartItems;
            _removeFromCart = removeFromCart;
            _updateQuantity = updateQuantity;
            _userManager = userManager;
        }

        public List<SellerGroup> SellerGroups { get; set; } = new();
        public decimal CartTotal { get; set; }

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            var items = await _getCartItems.ExecuteAsync(userId);
            SellerGroups = items
                .GroupBy(i => new { i.SellerId, i.SellerName })
                .Select(g => new SellerGroup(g.Key.SellerId, g.Key.SellerName, g.ToList()))
                .ToList();
            CartTotal = items.Sum(i => i.UnitPrice * i.Quantity);
        }

        public async Task<IActionResult> OnPostRemoveAsync(int itemId)
        {
            await _removeFromCart.ExecuteAsync(itemId);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateQuantityAsync(int itemId, int quantity)
        {
            if (quantity >= 1)
            {
                await _updateQuantity.ExecuteAsync(itemId, quantity);
            }
            return RedirectToPage();
        }

        public record SellerGroup(string SellerId, string SellerName, List<CartItemModel> Items);
    }
}
