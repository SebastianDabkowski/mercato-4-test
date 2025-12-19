using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller.Products
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GetProducts _getProducts;
        private readonly DeleteProduct _deleteProduct;

        public IndexModel(UserManager<ApplicationUser> userManager, GetProducts getProducts, DeleteProduct deleteProduct)
        {
            _userManager = userManager;
            _getProducts = getProducts;
            _deleteProduct = deleteProduct;
        }

        public List<ProductModel> Products { get; private set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return;
            }

            Products = await _getProducts.GetBySeller(user.Id);
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var deleted = await _deleteProduct.ArchiveAsync(id, user.Id);
            if (!deleted)
            {
                return Forbid();
            }

            StatusMessage = "Product archived.";
            return RedirectToPage();
        }
    }
}
