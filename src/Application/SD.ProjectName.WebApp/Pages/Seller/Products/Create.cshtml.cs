using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller.Products
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class CreateModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CreateProduct _createProduct;
        private readonly CategoryManagement _categoryManagement;

        public CreateModel(UserManager<ApplicationUser> userManager, CreateProduct createProduct, CategoryManagement categoryManagement)
        {
            _userManager = userManager;
            _createProduct = createProduct;
            _categoryManagement = categoryManagement;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGet()
        {
            await LoadCategoriesAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadCategoriesAsync();

            ValidateCategoryAgainstOptions();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            await _createProduct.CreateAsync(new CreateProduct.Request
            {
                Title = Input.Title,
                Category = Input.Category,
                Description = Input.Description,
                Price = Input.Price,
                Stock = Input.Stock,
                ImageUrls = Input.ImageUrls,
                WeightKg = Input.WeightKg,
                LengthCm = Input.LengthCm,
                WidthCm = Input.WidthCm,
                HeightCm = Input.HeightCm,
                ShippingMethods = Input.ShippingMethods
            }, user.Id);

            StatusMessage = "Product saved as draft.";
            return RedirectToPage("./Index");
        }

        public IReadOnlyList<CategoryManagement.CategoryOption> CategoryOptions { get; private set; } = Array.Empty<CategoryManagement.CategoryOption>();

        private async Task LoadCategoriesAsync()
        {
            CategoryOptions = await _categoryManagement.GetActiveOptions();
        }

        private void ValidateCategoryAgainstOptions()
        {
            if (CategoryOptions.Any() && !CategoryOptions.Any(c => string.Equals(c.Name, Input.Category, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(Input.Category), "Select a valid category.");
            }
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
        }
    }
}
