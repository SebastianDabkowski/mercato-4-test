using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;
using SD.ProjectName.WebApp.Stores;

namespace SD.ProjectName.WebApp.Pages.Seller
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class StoreModel : PageModel
    {
        private const long MaxLogoSizeBytes = 1_048_576; // 1 MB
        private static readonly string[] AllowedLogoExtensions = [".png", ".jpg", ".jpeg"];
        private static readonly string[] AllowedLogoContentTypes = ["image/png", "image/jpeg"];
        private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];
        private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<StoreModel> _logger;

        public StoreModel(
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<StoreModel> logger)
        {
            _userManager = userManager;
            _environment = environment;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? LogoUrl { get; private set; }

        public bool RequiresKyc { get; private set; }

        public KycStatus KycStatus { get; private set; }

        public string? PublicStoreUrl { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(120, MinimumLength = 3)]
            [Display(Name = "Store name")]
            public string StoreName { get; set; } = string.Empty;

            [StringLength(1000)]
            [Display(Name = "Store description")]
            public string? Description { get; set; }

            [Required]
            [EmailAddress]
            [StringLength(320)]
            [Display(Name = "Contact email")]
            public string ContactEmail { get; set; } = string.Empty;

            [Phone]
            [StringLength(64)]
            [Display(Name = "Phone number")]
            public string? ContactPhone { get; set; }

            [Url]
            [StringLength(2048)]
            [Display(Name = "Website URL")]
            public string? WebsiteUrl { get; set; }

            [Display(Name = "Store logo")]
            public IFormFile? Logo { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (user.RequiresKyc && user.KycStatus != KycStatus.Approved)
            {
                TempData["StatusMessage"] = "Complete KYC to manage your store profile.";
                return RedirectToPage("/Seller/Kyc");
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;
            LogoUrl = user.StoreLogoPath;

            Input.StoreName = user.StoreName ?? string.Empty;
            Input.Description = user.StoreDescription ?? string.Empty;
            Input.ContactEmail = user.StoreContactEmail ?? user.Email ?? string.Empty;
            Input.ContactPhone = user.StoreContactPhone ?? user.PhoneNumber ?? string.Empty;
            Input.WebsiteUrl = user.StoreWebsiteUrl ?? string.Empty;

            SetPublicStoreUrl(user.StoreName);

            StatusMessage ??= "Keep your store details up to date so buyers can reach you.";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (user.RequiresKyc && user.KycStatus != KycStatus.Approved)
            {
                TempData["StatusMessage"] = "Complete KYC to manage your store profile.";
                return RedirectToPage("/Seller/Kyc");
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;
            LogoUrl = user.StoreLogoPath;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var trimmedStoreName = Input.StoreName.Trim();

            var duplicate = await _userManager.Users
                .Where(u => u.Id != user.Id && u.StoreName != null)
                .AnyAsync(u => EF.Functions.Collate(u.StoreName!, "NOCASE") == EF.Functions.Collate(trimmedStoreName, "NOCASE"));

            if (duplicate)
            {
                ModelState.AddModelError("Input.StoreName", "Store name is already in use.");
                return Page();
            }

            var logoPath = user.StoreLogoPath;
            if (Input.Logo is { Length: > 0 })
            {
                var extension = Path.GetExtension(Input.Logo.FileName).ToLowerInvariant();
                var contentType = (Input.Logo.ContentType ?? string.Empty).ToLowerInvariant();

                if (Input.Logo.Length > MaxLogoSizeBytes)
                {
                    ModelState.AddModelError("Input.Logo", "Logo must be 1 MB or smaller.");
                    return Page();
                }

                if (!AllowedLogoExtensions.Contains(extension) || !AllowedLogoContentTypes.Contains(contentType))
                {
                    ModelState.AddModelError("Input.Logo", "Logo must be a PNG or JPEG image.");
                    return Page();
                }

                await using var logoReadStream = Input.Logo.OpenReadStream();
                if (!HasValidImageSignature(logoReadStream, extension))
                {
                    ModelState.AddModelError("Input.Logo", "Logo file is not a valid image.");
                    return Page();
                }

                var uploadsRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
                var logosPath = Path.Combine(uploadsRoot, "uploads", "logos");
                Directory.CreateDirectory(logosPath);

                if (!string.IsNullOrWhiteSpace(user.StoreLogoPath))
                {
                    var existingFile = Path.Combine(uploadsRoot, user.StoreLogoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(existingFile))
                    {
                        try
                        {
                            System.IO.File.Delete(existingFile);
                        }
                        catch (IOException ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete previous store logo for user {UserId}", user.Id);
                        }
                    }
                }

                var fileName = Path.GetFileName($"{user.Id}-logo-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}");
                var filePath = Path.Combine(logosPath, fileName);
                var fullPath = Path.GetFullPath(filePath);
                var logosRoot = Path.GetFullPath(logosPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                if (!fullPath.StartsWith(logosRoot, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("Input.Logo", "Invalid logo path.");
                    return Page();
                }

                logoReadStream.Position = 0;
                await using (var stream = System.IO.File.Create(fullPath))
                {
                    await logoReadStream.CopyToAsync(stream);
                }

                logoPath = $"/uploads/logos/{fileName}";
            }

            user.StoreName = trimmedStoreName;
            user.StoreDescription = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
            user.StoreContactEmail = Input.ContactEmail.Trim();
            user.StoreContactPhone = string.IsNullOrWhiteSpace(Input.ContactPhone) ? null : Input.ContactPhone.Trim();
            user.StoreWebsiteUrl = string.IsNullOrWhiteSpace(Input.WebsiteUrl) ? null : Input.WebsiteUrl.Trim();
            user.StoreLogoPath = logoPath;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            TempData["StatusMessage"] = "Store profile saved.";

            return RedirectToPage();
        }

        private static bool HasValidImageSignature(Stream stream, string extension)
        {
            Span<byte> header = stackalloc byte[8];
            var read = stream.Read(header);
            var sig = header[..read];

            return extension switch
            {
                ".png" => sig.StartsWith(PngSignature),
                ".jpg" or ".jpeg" => sig.StartsWith(JpegSignature),
                _ => false
            };
        }

        private void SetPublicStoreUrl(string? storeName)
        {
            if (string.IsNullOrWhiteSpace(storeName))
            {
                PublicStoreUrl = null;
                return;
            }

            var slug = StoreUrlHelper.ToSlug(storeName);
            if (string.IsNullOrWhiteSpace(slug))
            {
                PublicStoreUrl = null;
                return;
            }

            PublicStoreUrl = Url.Page("/Stores/Profile", pageHandler: null, values: new { storeSlug = slug }, protocol: Request.Scheme);
        }
    }
}
