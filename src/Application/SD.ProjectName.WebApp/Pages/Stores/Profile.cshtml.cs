using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.Modules.Products.Application;
using SD.ProjectName.Modules.Products.Domain;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Stores;

namespace SD.ProjectName.WebApp.Pages.Stores
{
    public class ProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GetProducts _getProducts;

        public ProfileModel(UserManager<ApplicationUser> userManager, GetProducts getProducts)
        {
            _userManager = userManager;
            _getProducts = getProducts;
        }

        public string StoreName { get; private set; } = string.Empty;

        public string? StoreDescription { get; private set; }

        public string ContactEmail { get; private set; } = string.Empty;

        public string? ContactPhone { get; private set; }

        public string? WebsiteUrl { get; private set; }

        public string? LogoUrl { get; private set; }

        public List<ProductModel> ProductPreviews { get; private set; } = new();

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
            ProductPreviews = (await _getProducts.GetList())
                .Take(3)
                .ToList();

            return Page();
        }
    }
}
