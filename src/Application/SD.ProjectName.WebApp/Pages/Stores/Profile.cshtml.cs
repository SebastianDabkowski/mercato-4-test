using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.WebApp.Pages.Stores
{
    public class ProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string StoreName { get; private set; } = string.Empty;

        public string? StoreDescription { get; private set; }

        public string ContactEmail { get; private set; } = string.Empty;

        public string? ContactPhone { get; private set; }

        public string? WebsiteUrl { get; private set; }

        public string? LogoUrl { get; private set; }

        public async Task<IActionResult> OnGetAsync(string storeName)
        {
            if (string.IsNullOrWhiteSpace(storeName))
            {
                return NotFound();
            }

            var trimmed = storeName.Trim();

            var storeOwner = await _userManager.Users
                .AsNoTracking()
                .Where(u => u.StoreName != null && u.AccountType == AccountType.Seller)
                .FirstOrDefaultAsync(u => EF.Functions.Collate(u.StoreName!, "NOCASE") == EF.Functions.Collate(trimmed, "NOCASE"));

            if (storeOwner is null)
            {
                return NotFound();
            }

            StoreName = storeOwner.StoreName!;
            StoreDescription = storeOwner.StoreDescription;
            ContactEmail = storeOwner.StoreContactEmail ?? storeOwner.Email ?? string.Empty;
            ContactPhone = storeOwner.StoreContactPhone ?? storeOwner.PhoneNumber;
            WebsiteUrl = storeOwner.StoreWebsiteUrl;
            LogoUrl = storeOwner.StoreLogoPath;

            return Page();
        }
    }
}
