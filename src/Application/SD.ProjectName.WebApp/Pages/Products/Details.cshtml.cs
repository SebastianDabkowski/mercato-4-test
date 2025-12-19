using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;

namespace SD.ProjectName.WebApp.Pages.Products
{
    public class DetailsModel : PageModel
    {
        private readonly GetProducts _getProducts;

        public DetailsModel(GetProducts getProducts)
        {
            _getProducts = getProducts;
        }

        public ProductModel? Product { get; private set; }

        public List<string> ImageUrls { get; } = new();

        public List<string> ShippingMethods { get; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _getProducts.GetById(id, includeDrafts: false);
            if (Product is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(Product.ImageUrls))
            {
                ImageUrls.AddRange(Product.ImageUrls
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i.Trim())
                    .Where(i => !string.IsNullOrWhiteSpace(i)));
            }

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
