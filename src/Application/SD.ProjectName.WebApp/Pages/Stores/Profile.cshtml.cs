using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Stores;
using SD.ProjectName.WebApp.Services;

namespace SD.ProjectName.WebApp.Pages.Stores
{
    public class ProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GetProducts _getProducts;
        private readonly ProductImageService _imageService;

        public ProfileModel(UserManager<ApplicationUser> userManager, GetProducts getProducts, ProductImageService imageService)
        {
            _userManager = userManager;
            _getProducts = getProducts;
            _imageService = imageService;
        }

        public string StoreName { get; private set; } = string.Empty;

        public string? StoreDescription { get; private set; }

        public string ContactEmail { get; private set; } = string.Empty;

        public string? ContactPhone { get; private set; }

        public string? WebsiteUrl { get; private set; }

        public string? LogoUrl { get; private set; }

        public List<StoreProductPreview> ProductPreviews { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync(string storeSlug)
        {
            if (string.IsNullOrWhiteSpace(storeSlug))
            {
                return NotFound();
            }

            var slug = StoreUrlHelper.ToSlug(storeSlug);
            if (string.IsNullOrWhiteSpace(slug))
            {
                return NotFound();
            }

            var storeOwner = (await _userManager.Users
                .AsNoTracking()
                .Where(u => u.StoreName != null && u.AccountType == AccountType.Seller)
                .ToListAsync())
                .FirstOrDefault(u => string.Equals(StoreUrlHelper.ToSlug(u.StoreName!), slug, StringComparison.OrdinalIgnoreCase));

            if (storeOwner is null)
            {
                return NotFound();
            }

            if (storeOwner.AccountStatus == AccountStatus.Suspended ||
                storeOwner.AccountStatus == AccountStatus.Unverified)
            {
                return NotFound();
            }

            StoreName = storeOwner.StoreName!;
            StoreDescription = storeOwner.StoreDescription;
            ContactEmail = storeOwner.StoreContactEmail ?? storeOwner.Email ?? string.Empty;
            ContactPhone = storeOwner.StoreContactPhone ?? storeOwner.PhoneNumber;
            WebsiteUrl = storeOwner.StoreWebsiteUrl;
            LogoUrl = storeOwner.StoreLogoPath;
            var products = (await _getProducts.GetBySeller(storeOwner.Id, includeDrafts: false))
                .Take(3)
                .ToList();

            ProductPreviews = products
                .Select(p =>
                {
                    var main = _imageService.GetMainImage(p.ImageUrls);
                    return new StoreProductPreview(
                        p,
                        _imageService.GetVariant(main, ImageVariant.Thumbnail));
                })
                .ToList();

            return Page();
        }

        public record StoreProductPreview(ProductModel Product, string? ThumbnailUrl);
    }
}
