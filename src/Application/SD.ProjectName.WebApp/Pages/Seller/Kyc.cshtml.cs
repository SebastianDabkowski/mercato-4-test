using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class KycModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public KycModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public KycStatus KycStatus { get; private set; }

        public bool RequiresKyc { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public bool IsSubmissionLocked => KycStatus == KycStatus.Pending;

        public class InputModel : IValidatableObject
        {
            [Required]
            [Display(Name = "Seller type")]
            public SellerType? SellerType { get; set; }

            [StringLength(200)]
            [Display(Name = "Company name")]
            public string? CompanyName { get; set; }

            [StringLength(100)]
            [Display(Name = "Registration number")]
            [RegularExpression(@"^[A-Za-z0-9\-]{3,}$", ErrorMessage = "Registration number must be at least 3 characters and contain only letters, digits, or hyphens.")]
            public string? RegistrationNumber { get; set; }

            [StringLength(50)]
            [Display(Name = "Tax ID")]
            [RegularExpression(@"^[A-Za-z0-9\-]{3,}$", ErrorMessage = "Tax ID must be at least 3 characters and contain only letters, digits, or hyphens.")]
            public string? TaxId { get; set; }

            [StringLength(150)]
            [Display(Name = "Contact person")]
            public string? ContactPerson { get; set; }

            [StringLength(200)]
            [Display(Name = "Full name")]
            public string? FullName { get; set; }

            [StringLength(100)]
            [Display(Name = "Personal ID number")]
            [RegularExpression(@"^[A-Za-z0-9\-]{5,}$", ErrorMessage = "Personal ID number must be at least 5 characters and contain only letters, digits, or hyphens.")]
            public string? PersonalIdNumber { get; set; }

            [Required]
            [StringLength(300)]
            [Display(Name = "Registered address")]
            public string RegisteredAddress { get; set; } = string.Empty;

            [Required]
            [Phone]
            [StringLength(64)]
            [Display(Name = "Contact phone")]
            public string ContactPhone { get; set; } = string.Empty;

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (SellerType is null)
                {
                    yield return new ValidationResult("Select whether you operate as a company or an individual.", new[] { nameof(SellerType) });
                    yield break;
                }

                if (SellerType == Data.SellerType.Company)
                {
                    if (string.IsNullOrWhiteSpace(CompanyName))
                    {
                        yield return new ValidationResult("Company name is required for company sellers.", new[] { nameof(CompanyName) });
                    }

                    if (string.IsNullOrWhiteSpace(RegistrationNumber))
                    {
                        yield return new ValidationResult("Registration number is required for company sellers.", new[] { nameof(RegistrationNumber) });
                    }

                    if (string.IsNullOrWhiteSpace(TaxId))
                    {
                        yield return new ValidationResult("Tax ID is required for company sellers.", new[] { nameof(TaxId) });
                    }

                    if (string.IsNullOrWhiteSpace(ContactPerson))
                    {
                        yield return new ValidationResult("Contact person is required for company sellers.", new[] { nameof(ContactPerson) });
                    }
                }
                else if (SellerType == Data.SellerType.Individual)
                {
                    if (string.IsNullOrWhiteSpace(FullName))
                    {
                        yield return new ValidationResult("Full name is required for individual sellers.", new[] { nameof(FullName) });
                    }

                    if (string.IsNullOrWhiteSpace(PersonalIdNumber))
                    {
                        yield return new ValidationResult("Personal ID number is required for individual sellers.", new[] { nameof(PersonalIdNumber) });
                    }
                }

                if (string.IsNullOrWhiteSpace(RegisteredAddress))
                {
                    yield return new ValidationResult("Registered address is required.", new[] { nameof(RegisteredAddress) });
                }

                if (string.IsNullOrWhiteSpace(ContactPhone))
                {
                    yield return new ValidationResult("Provide a contact phone number.", new[] { nameof(ContactPhone) });
                }
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (!user.OnboardingCompleted)
            {
                return RedirectToPage("/Seller/Onboarding");
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;

            if (!user.RequiresKyc || user.KycStatus == KycStatus.Approved)
            {
                return RedirectToPage("/Seller/Dashboard");
            }

            Input.SellerType = ResolveSellerType(user);
            Input.CompanyName = user.CompanyName ?? string.Empty;
            Input.TaxId = user.TaxId ?? string.Empty;
            Input.RegistrationNumber = user.VerificationRegistrationNumber ?? string.Empty;
            Input.FullName = $"{user.FirstName} {user.LastName}".Trim();
            Input.PersonalIdNumber = user.VerificationPersonalIdNumber ?? string.Empty;
            Input.RegisteredAddress = user.VerificationAddress ?? string.Empty;
            Input.ContactPhone = user.PhoneNumber ?? string.Empty;
            Input.ContactPerson = user.VerificationContactPerson ?? Input.FullName;

            StatusMessage ??= KycStatus switch
            {
                KycStatus.Pending => "Your verification is pending review. We will notify you once it is reviewed.",
                KycStatus.Rejected => "Your verification was rejected. Update your details to resubmit.",
                _ => "Complete KYC to unlock seller tools."
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (!user.OnboardingCompleted)
            {
                return RedirectToPage("/Seller/Onboarding");
            }

            RequiresKyc = user.RequiresKyc;
            KycStatus = user.KycStatus;

            if (!user.RequiresKyc || user.KycStatus == KycStatus.Approved)
            {
                return RedirectToPage("/Seller/Dashboard");
            }

            if (user.KycStatus == KycStatus.Pending)
            {
                StatusMessage = "Your verification is pending review. We will notify you once it is completed.";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                StatusMessage = "Provide all required details to continue with KYC.";
                return Page();
            }

            user.SellerType = Input.SellerType!.Value;
            user.CompanyName = Input.SellerType == SellerType.Company ? Input.CompanyName?.Trim() : null;
            user.TaxId = Input.SellerType == SellerType.Company ? Input.TaxId?.Trim() : null;
            user.VerificationRegistrationNumber = Input.SellerType == SellerType.Company ? Input.RegistrationNumber?.Trim() : null;
            user.VerificationPersonalIdNumber = Input.SellerType == SellerType.Individual ? Input.PersonalIdNumber?.Trim() : null;
            user.VerificationContactPerson = Input.SellerType == SellerType.Company ? Input.ContactPerson?.Trim() : Input.FullName?.Trim();
            user.VerificationAddress = Input.RegisteredAddress.Trim();
            user.PhoneNumber = Input.ContactPhone.Trim();
            user.KycStatus = KycStatus.Pending;
            user.KycSubmittedAt = DateTimeOffset.UtcNow;
            user.KycApprovedAt = null;

            await _userManager.UpdateAsync(user);

            TempData["StatusMessage"] = "Your verification request was submitted and is now pending review.";

            return RedirectToPage();
        }

        private static SellerType ResolveSellerType(ApplicationUser user)
        {
            if (!string.IsNullOrWhiteSpace(user.CompanyName))
            {
                return SellerType.Company;
            }

            return user.SellerType switch
            {
                SellerType.Company or SellerType.Individual => user.SellerType,
                _ => SellerType.Individual
            };
        }
    }
}
