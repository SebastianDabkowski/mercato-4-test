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
        private readonly BulkUpdateProducts _bulkUpdateProducts;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            GetProducts getProducts,
            DeleteProduct deleteProduct,
            BulkUpdateProducts bulkUpdateProducts)
        {
            _userManager = userManager;
            _getProducts = getProducts;
            _deleteProduct = deleteProduct;
            _bulkUpdateProducts = bulkUpdateProducts;
        }

        public List<ProductModel> Products { get; private set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public BulkUpdateInput BulkUpdate { get; set; } = new();

        public BulkUpdateProducts.Response? BulkResult { get; private set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return;
            }

            Products = await _getProducts.GetBySeller(user.Id);
        }

        public async Task<IActionResult> OnPostBulkUpdateAsync(string submitAction)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            Products = await _getProducts.GetBySeller(user.Id);

            if (BulkUpdate.ProductIds is null || !BulkUpdate.ProductIds.Any())
            {
                ModelState.AddModelError(nameof(BulkUpdate.ProductIds), "Select at least one product.");
            }

            if (BulkUpdate.PriceOperation == BulkUpdateProducts.PriceOperation.None
                && BulkUpdate.StockOperation == BulkUpdateProducts.StockOperation.None)
            {
                ModelState.AddModelError(string.Empty, "Select a price or stock change.");
            }

            var isPricePercent = BulkUpdate.PriceOperation is BulkUpdateProducts.PriceOperation.IncreaseByPercentage
                or BulkUpdateProducts.PriceOperation.DecreaseByPercentage;
            if (BulkUpdate.PriceOperation != BulkUpdateProducts.PriceOperation.None && (BulkUpdate.PriceValue is null || (!isPricePercent && BulkUpdate.PriceValue <= 0)))
            {
                ModelState.AddModelError(nameof(BulkUpdate.PriceValue), "Enter a price amount greater than zero.");
            }

            if (BulkUpdate.StockOperation != BulkUpdateProducts.StockOperation.None && BulkUpdate.StockValue is null)
            {
                ModelState.AddModelError(nameof(BulkUpdate.StockValue), "Enter a stock amount.");
            }

            if (BulkUpdate.StockValue is < 0)
            {
                ModelState.AddModelError(nameof(BulkUpdate.StockValue), "Stock change cannot be negative.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var previewOnly = string.Equals(submitAction, "preview", StringComparison.OrdinalIgnoreCase);

            var request = new BulkUpdateProducts.Request
            {
                ProductIds = BulkUpdate.ProductIds ?? new List<int>(),
                PriceOperation = BulkUpdate.PriceOperation,
                PriceValue = BulkUpdate.PriceValue ?? 0,
                StockOperation = BulkUpdate.StockOperation,
                StockValue = BulkUpdate.StockValue ?? 0,
                ApplyChanges = !previewOnly
            };

            BulkResult = await _bulkUpdateProducts.ApplyAsync(request, user.Id);

            if (previewOnly)
            {
                StatusMessage = "Preview the changes before applying.";
                return Page();
            }

            if (BulkResult.Failed.Any())
            {
                Products = await _getProducts.GetBySeller(user.Id);
                StatusMessage = $"{BulkResult.Updated.Count} products updated. {BulkResult.Failed.Count} could not be updated.";
                return Page();
            }

            Products = await _getProducts.GetBySeller(user.Id);
            StatusMessage = $"Updated {BulkResult.Updated.Count} products.";
            return RedirectToPage();
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

        public class BulkUpdateInput
        {
            public List<int> ProductIds { get; set; } = new();

            public BulkUpdateProducts.PriceOperation PriceOperation { get; set; } = BulkUpdateProducts.PriceOperation.None;

            public decimal? PriceValue { get; set; }

            public BulkUpdateProducts.StockOperation StockOperation { get; set; } = BulkUpdateProducts.StockOperation.None;

            public int? StockValue { get; set; }
        }
    }
}
