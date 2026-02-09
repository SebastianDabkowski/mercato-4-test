using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Stores;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Products
{
    public class DetailsModel : PageModel
    {
        private const int RecentlyViewedMaxItemsLimit = 5;
        private readonly GetProducts _getProducts;
        private readonly ProductImageService _imageService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AddToCart _addToCart;
        private readonly ICartIdentityService _cartIdentityService;

        public DetailsModel(GetProducts getProducts, ProductImageService imageService, UserManager<ApplicationUser> userManager, AddToCart addToCart, ICartIdentityService cartIdentityService)
        {
            _getProducts = getProducts;
            _imageService = imageService;
            _userManager = userManager;
            _addToCart = addToCart;
            _cartIdentityService = cartIdentityService;
        }

        public ProductModel? Product { get; private set; }

        public List<ProductImageView> Images { get; } = new();

        public List<string> ShippingMethods { get; } = new();

        public string? ThumbnailUrl { get; private set; }

        public int RecentlyViewedMaxItems => RecentlyViewedMaxItemsLimit;

        public string? BackToResultsUrl { get; private set; }

        public string? CategoryUrl { get; private set; }

        public string? SellerStoreUrl { get; private set; }

        public string? SellerStoreName { get; private set; }

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

            // Set back to results URL from referrer if it's from search or category pages
            var referrer = Request.Headers.Referer.ToString();
            if (!string.IsNullOrWhiteSpace(referrer) && Uri.TryCreate(referrer, UriKind.Absolute, out var referrerUri))
            {
                var path = referrerUri.PathAndQuery;
                if (path.StartsWith("/Search/Index", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/Products/List", StringComparison.OrdinalIgnoreCase))
                {
                    BackToResultsUrl = path;
                }
            }

            // Set category URL
            if (!string.IsNullOrWhiteSpace(Product.Category))
            {
                CategoryUrl = Url.Page("/Products/List", new { category = Product.Category });
            }

            // Set seller store URL
            if (!string.IsNullOrWhiteSpace(Product.SellerId))
            {
                var seller = await _userManager.FindByIdAsync(Product.SellerId);
                if (seller is not null && !string.IsNullOrWhiteSpace(seller.StoreName))
                {
                    var storeSlug = StoreUrlHelper.ToSlug(seller.StoreName);
                    if (!string.IsNullOrWhiteSpace(storeSlug))
                    {
                        SellerStoreName = seller.StoreName;
                        SellerStoreUrl = Url.Page("/Stores/Profile", new { storeSlug });
                    }
                }
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

        public async Task<IActionResult> OnPostAddToCartAsync(int id)
        {
            var product = await _getProducts.GetById(id, includeDrafts: false);
            if (product is null)
            {
                return NotFound();
            }

            var seller = await _userManager.FindByIdAsync(product.SellerId);
            var sellerName = seller?.StoreName ?? string.Empty;

            var buyerId = _cartIdentityService.GetOrCreateBuyerId();

            await _addToCart.ExecuteAsync(
                buyerId,
                product.Id,
                product.Name,
                product.Category,
                product.Price,
                product.SellerId,
                sellerName);
            return RedirectToPage("/Buyer/Cart");
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
