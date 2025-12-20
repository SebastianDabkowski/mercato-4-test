using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain.Interfaces;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Seller.Products
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class CreateModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CreateProduct _createProduct;
        private readonly CategoryManagement _categoryManagement;
        private readonly IProductRepository _productRepository;
        private readonly ProductImageService _imageService;

        public CreateModel(
            UserManager<ApplicationUser> userManager,
            CreateProduct createProduct,
            CategoryManagement categoryManagement,
            IProductRepository productRepository,
            ProductImageService imageService)
        {
            _userManager = userManager;
            _createProduct = createProduct;
            _categoryManagement = categoryManagement;
            _productRepository = productRepository;
            _imageService = imageService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public IReadOnlyList<ProductImageView> ExistingImages { get; private set; } = Array.Empty<ProductImageView>();

        public async Task OnGet()
        {
            await LoadCategoriesAsync();
            ExistingImages = _imageService.BuildViews(Input.ImageUrls);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadCategoriesAsync();

            ValidateCategoryAgainstOptions();
            _imageService.ValidateUploads(Input.Uploads, ModelState, nameof(Input.Uploads));

            if (!ModelState.IsValid)
            {
                ExistingImages = _imageService.BuildViews(Input.ImageUrls);
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var manualImages = _imageService.Parse(Input.ImageUrls);

            var product = await _createProduct.CreateAsync(new CreateProduct.Request
            {
                Title = Input.Title,
                Category = Input.Category,
                Description = Input.Description,
                Price = Input.Price,
                Stock = Input.Stock,
                ImageUrls = _imageService.BuildMultiline(manualImages),
                WeightKg = Input.WeightKg,
                LengthCm = Input.LengthCm,
                WidthCm = Input.WidthCm,
                HeightCm = Input.HeightCm,
                ShippingMethods = Input.ShippingMethods
            }, user.Id);

            var uploadedImages = await _imageService.SaveUploadsAsync(product.Id, Input.Uploads);
            var merged = _imageService.MergeAndOrderImages(manualImages, uploadedImages, Input.MainImage);
            if (!string.IsNullOrWhiteSpace(merged) && !string.Equals(product.ImageUrls, merged, StringComparison.Ordinal))
            {
                product.ImageUrls = merged;
                await _productRepository.Update(product);
            }

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
            [Display(Name = "Existing image URLs (optional)")]
            public string? ImageUrls { get; set; }

            [Display(Name = "Product images")]
            public List<IFormFile> Uploads { get; set; } = new();

            [Display(Name = "Main image")]
            public string? MainImage { get; set; }

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
