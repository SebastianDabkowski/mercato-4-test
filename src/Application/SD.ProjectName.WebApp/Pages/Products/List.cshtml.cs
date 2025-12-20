using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Products
{
    public class ListModel : PageModel
    {
        private readonly ILogger<ListModel> _logger;
        private readonly GetProducts _getProducts;
        private readonly ProductImageService _imageService;

        public ListModel(ILogger<ListModel> logger, GetProducts getProducts, ProductImageService imageService)
        {
            _logger = logger;
            _getProducts = getProducts;
            _imageService = imageService;
        }

        public List<ProductListItem> Products { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Category { get; set; }

        public async Task OnGetAsync()
        {
            var products = await _getProducts.GetList(Category);
            Products = products.Select(p =>
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
