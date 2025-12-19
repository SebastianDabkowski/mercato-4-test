using System.ComponentModel.DataAnnotations;
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
    public class EditModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GetProducts _getProducts;
        private readonly UpdateProduct _updateProduct;

        public EditModel(UserManager<ApplicationUser> userManager, GetProducts getProducts, UpdateProduct updateProduct)
        {
            _userManager = userManager;
            _getProducts = getProducts;
            _updateProduct = updateProduct;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var product = await _getProducts.GetById(id);
            if (product is null || product.SellerId != user.Id)
            {
                return NotFound();
            }

            Input = new InputModel
            {
                Title = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                Category = product.Category,
                ImageUrls = product.ImageUrls,
                WeightKg = product.WeightKg,
                LengthCm = product.LengthCm,
                WidthCm = product.WidthCm,
                HeightCm = product.HeightCm,
                ShippingMethods = product.ShippingMethods,
                Publish = product.Status == ProductStatuses.Active
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var updated = await _updateProduct.UpdateAsync(id, new UpdateProduct.Request
            {
                Title = Input.Title,
                Description = Input.Description,
                Price = Input.Price,
                Stock = Input.Stock,
                Category = Input.Category,
                ImageUrls = Input.ImageUrls,
                WeightKg = Input.WeightKg,
                LengthCm = Input.LengthCm,
                WidthCm = Input.WidthCm,
                HeightCm = Input.HeightCm,
                ShippingMethods = Input.ShippingMethods,
                Publish = Input.Publish
            }, user.Id);

            if (updated is null)
            {
                return NotFound();
            }

            StatusMessage = "Product updated.";
            return RedirectToPage("./Index");
        }

        public class InputModel
        {
            [Required]
            [StringLength(200, MinimumLength = 3)]
            [Display(Name = "Title")]
            public string Title { get; set; } = string.Empty;

            [StringLength(2000)]
            [Display(Name = "Description")]
            public string? Description { get; set; }

            [Required]
            [Range(0.01, 1_000_000)]
            [Display(Name = "Price")]
            public decimal Price { get; set; }

            [Required]
            [Range(0, 1_000_000)]
            [Display(Name = "Stock")]
            public int Stock { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 2)]
            [Display(Name = "Category")]
            public string Category { get; set; } = string.Empty;

            [StringLength(4000)]
            [Display(Name = "Image URLs (one per line)")]
            public string? ImageUrls { get; set; }

            [Range(0.0, 1000.0)]
            [Display(Name = "Weight (kg)")]
            public decimal? WeightKg { get; set; }

            [Range(0.0, 500.0)]
            [Display(Name = "Length (cm)")]
            public decimal? LengthCm { get; set; }

            [Range(0.0, 500.0)]
            [Display(Name = "Width (cm)")]
            public decimal? WidthCm { get; set; }

            [Range(0.0, 500.0)]
            [Display(Name = "Height (cm)")]
            public decimal? HeightCm { get; set; }

            [StringLength(1000)]
            [Display(Name = "Shipping methods (one per line)")]
            public string? ShippingMethods { get; set; }

            [Display(Name = "Make product visible to buyers")]
            public bool Publish { get; set; }
        }
    }
}
