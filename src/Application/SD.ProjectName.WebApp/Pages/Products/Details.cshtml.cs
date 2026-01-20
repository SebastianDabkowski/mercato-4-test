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
        private const int RecentlyViewedMaxItemsLimit = 5;
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

        public string? ThumbnailUrl { get; private set; }

        public int RecentlyViewedMaxItems => RecentlyViewedMaxItemsLimit;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _getProducts.GetById(id, includeDrafts: false);
            if (Product is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Page();
            }

            Images.AddRange(_imageService.BuildViews(Product.ImageUrls));
            var mainImage = _imageService.GetMainImage(Product.ImageUrls);
            if (!string.IsNullOrWhiteSpace(mainImage))
            {
                ThumbnailUrl = _imageService.GetVariant(mainImage, ImageVariant.Thumbnail);
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

        public async Task<IActionResult> OnGetRecentlyViewedAsync(int id, string? ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return new JsonResult(Array.Empty<RecentlyViewedItem>());
            }

            var parsedIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
                .Where(value => value > 0)
                .ToList();

            if (!parsedIds.Any())
            {
                return new JsonResult(Array.Empty<RecentlyViewedItem>());
            }

            var lookup = new Dictionary<int, RecentlyViewedItem>();
            foreach (var productId in parsedIds)
            {
                var product = await _getProducts.GetById(productId, includeDrafts: false);
                if (product is null)
                {
                    continue;
                }

                var url = Url.Page("/Products/Details", new { id = product.Id }) ?? string.Empty;
                var main = _imageService.GetMainImage(product.ImageUrls);
                var thumbnail = string.IsNullOrWhiteSpace(main)
                    ? string.Empty
                    : _imageService.GetVariant(main, ImageVariant.Thumbnail);

                lookup[product.Id] = new RecentlyViewedItem(
                    product.Id,
                    product.Name,
                    product.Category,
                    product.Price.ToString("C"),
                    url,
                    thumbnail,
                    DateTimeOffset.UtcNow.ToString("O"));
            }

            var ordered = parsedIds
                .Where(lookup.ContainsKey)
                .Select(idValue => lookup[idValue])
                .Take(RecentlyViewedMaxItemsLimit)
                .ToList();

            return new JsonResult(ordered);
        }

        public record RecentlyViewedItem(
            int Id,
            string Name,
            string Category,
            string Price,
            string Url,
            string Image,
            string ViewedAt);
    }
}
