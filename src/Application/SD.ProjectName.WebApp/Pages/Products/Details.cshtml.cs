using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Products
{
    public class DetailsModel : PageModel
    {
        private readonly GetProducts _getProducts;
        private readonly ProductImageService _imageService;

        public DetailsModel(GetProducts getProducts, ProductImageService imageService)
        {
            _getProducts = getProducts;
            _imageService = imageService;
        }

        public ProductModel? Product { get; private set; }

        public List<ProductImageView> Images { get; } = new();

        public List<string> ShippingMethods { get; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _getProducts.GetById(id, includeDrafts: false);
            if (Product is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Page();
            }

            Images.AddRange(_imageService.BuildViews(Product.ImageUrls));

            if (!string.IsNullOrWhiteSpace(Product.ShippingMethods))
            {
                ShippingMethods.AddRange(Product.ShippingMethods
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrWhiteSpace(m)));
            }

            return Page();
        }
    }
}
