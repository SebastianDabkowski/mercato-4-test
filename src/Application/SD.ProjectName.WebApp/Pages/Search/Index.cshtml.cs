using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Search
{
    public class IndexModel : PageModel
    {
        private readonly GetProducts _getProducts;
        private readonly ProductImageService _imageService;

        public IndexModel(GetProducts getProducts, ProductImageService imageService)
        {
            _getProducts = getProducts;
            _imageService = imageService;
        }

        [BindProperty(SupportsGet = true)]
        public string? Q { get; set; }

        public List<ProductListItem> Results { get; private set; } = new();

        public bool HasQuery => !string.IsNullOrWhiteSpace(Q);

        public async Task OnGetAsync()
        {
            if (!HasQuery)
            {
                Results.Clear();
                return;
            }

            var normalized = Q!.Trim();
            if (normalized.Length > 200)
            {
                normalized = normalized[..200];
            }

            Q = normalized;

            var products = await _getProducts.Search(normalized);
            Results = products.Select(p =>
            {
                var main = _imageService.GetMainImage(p.ImageUrls);
                return new ProductListItem(
                    p,
                    main,
                    _imageService.GetVariant(main, ImageVariant.Thumbnail));
            }).ToList();
        }

        public record ProductListItem(ProductModel Product, string? MainImageUrl, string? ThumbnailUrl);
    }
}
