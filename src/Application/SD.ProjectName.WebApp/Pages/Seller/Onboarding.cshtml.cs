using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.WebApp.Pages.Seller
{
    [Authorize(Roles = IdentityRoles.Seller)]
    public class OnboardingModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<OnboardingModel> _logger;

        private static readonly Dictionary<OnboardingStep, OnboardingStep> NextStepMap = new()
        {
            { OnboardingStep.None, OnboardingStep.StoreProfile },
            { OnboardingStep.StoreProfile, OnboardingStep.Verification },
            { OnboardingStep.Verification, OnboardingStep.Payout },
            { OnboardingStep.Payout, OnboardingStep.Completed },
            { OnboardingStep.Completed, OnboardingStep.Completed }
        };

        public OnboardingModel(UserManager<ApplicationUser> userManager, ILogger<OnboardingModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public int Step { get; private set; }

        public bool IsCompleted { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public class InputModel
        {
            public int Step { get; set; }

            // Store profile
            [Display(Name = "Store name")]
            public string StoreName { get; set; } = string.Empty;

            [Display(Name = "Store description")]
            public string? Description { get; set; }

            [Display(Name = "Contact email")]
            public string ContactEmail { get; set; } = string.Empty;

            [Display(Name = "Phone number")]
            public string? ContactPhone { get; set; }

            [Display(Name = "Website URL")]
            public string? WebsiteUrl { get; set; }

            // Verification
            [Display(Name = "Seller type")]
            public SellerType? SellerType { get; set; }

            [Display(Name = "Company name")]
            public string? CompanyName { get; set; }

            [Display(Name = "Registration number")]
            public string? RegistrationNumber { get; set; }

            [Display(Name = "Tax ID")]
            public string? TaxId { get; set; }

            [Display(Name = "Contact person")]
            public string? ContactPerson { get; set; }

            [Display(Name = "Full name")]
            public string? FullName { get; set; }

            [Display(Name = "Personal ID number")]
            public string? PersonalIdNumber { get; set; }

            [Display(Name = "Registered address")]
            public string RegisteredAddress { get; set; } = string.Empty;

            [Display(Name = "Contact phone")]
            public string ContactPhoneVerification { get; set; } = string.Empty;

            // Payout
            [Display(Name = "Payout beneficiary name")]
            public string PayoutBeneficiaryName { get; set; } = string.Empty;

            [Display(Name = "Payout account number")]
            public string PayoutAccountNumber { get; set; } = string.Empty;

            [Display(Name = "Bank name")]
            public string? PayoutBankName { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? step = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (user.AccountType != AccountType.Seller)
            {
                return RedirectToPage("/Index");
            }

            if (user.OnboardingCompleted)
            {
                return RedirectToPage("/Seller/Dashboard");
            }

            var activeStep = ResolveStep(user, step);
            Step = (int)activeStep;
            Input.Step = Step;
            IsCompleted = user.OnboardingCompleted;

            PopulateInputFromUser(user);

            StatusMessage ??= "Complete the onboarding wizard to activate your seller account.";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? step = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            if (user.AccountType != AccountType.Seller)
            {
                return RedirectToPage("/Index");
            }

            if (user.OnboardingCompleted)
            {
                return RedirectToPage("/Seller/Dashboard");
            }

            var currentStep = ResolveStep(user, step ?? Input.Step);

            Step = (int)currentStep;
            Input.Step = Step;

            switch (currentStep)
            {
                case OnboardingStep.StoreProfile:
                    ValidateStoreProfile();
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

                    user.StoreName = trimmedStoreName;
                    user.StoreDescription = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();
                    user.StoreContactEmail = Input.ContactEmail.Trim();
                    user.StoreContactPhone = string.IsNullOrWhiteSpace(Input.ContactPhone) ? null : Input.ContactPhone.Trim();
                    user.StoreWebsiteUrl = string.IsNullOrWhiteSpace(Input.WebsiteUrl) ? null : Input.WebsiteUrl.Trim();
                    user.OnboardingStep = OnboardingStep.StoreProfile;
                    break;

                case OnboardingStep.Verification:
                    ValidateVerification();
                    if (!ModelState.IsValid)
                    {
                        return Page();
                    }

                    user.SellerType = Input.SellerType!.Value;
                    user.CompanyName = Input.SellerType == SellerType.Company ? Input.CompanyName?.Trim() : null;
                    user.TaxId = Input.SellerType == SellerType.Company ? Input.TaxId?.Trim() : null;
                    user.VerificationRegistrationNumber = Input.SellerType == SellerType.Company ? Input.RegistrationNumber?.Trim() : null;
                    user.VerificationPersonalIdNumber = Input.SellerType == SellerType.Individual ? Input.PersonalIdNumber?.Trim() : null;
                    user.VerificationContactPerson = Input.SellerType == SellerType.Company ? Input.ContactPerson?.Trim() : Input.FullName?.Trim();
                    user.VerificationAddress = Input.RegisteredAddress.Trim();
                    user.PhoneNumber = Input.ContactPhoneVerification.Trim();
                    user.RequiresKyc = true;
                    user.OnboardingStep = OnboardingStep.Verification;
                    break;

                case OnboardingStep.Payout:
                    ValidatePayout();
                    if (!ModelState.IsValid)
                    {
                        return Page();
                    }

                    user.PayoutBeneficiaryName = Input.PayoutBeneficiaryName.Trim();
                    user.PayoutAccountNumber = Input.PayoutAccountNumber.Trim();
                    user.PayoutBankName = string.IsNullOrWhiteSpace(Input.PayoutBankName) ? null : Input.PayoutBankName.Trim();
                    user.OnboardingStep = OnboardingStep.Completed;
                    user.OnboardingCompleted = true;
                    user.RequiresKyc = true;
                    user.KycStatus = KycStatus.Pending;
                    user.KycSubmittedAt = DateTimeOffset.UtcNow;
                    user.KycApprovedAt = null;
                    user.AccountStatus = AccountStatus.Unverified;
                    break;

                default:
                    return RedirectToPage("/Seller/Dashboard");
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            if (user.OnboardingCompleted)
            {
                TempData["StatusMessage"] = "Your onboarding was submitted and is pending verification.";
                return RedirectToPage("/Seller/Dashboard");
            }

            var nextStep = NextStepMap[user.OnboardingStep];
            StatusMessage = "Progress saved. Continue to the next step.";

            return RedirectToPage(new { step = (int)nextStep });
        }

        private OnboardingStep ResolveStep(ApplicationUser user, int? requestedStep)
        {
            if (user.OnboardingCompleted)
            {
                return OnboardingStep.Completed;
            }

            var desired = NormalizeStep(requestedStep);
            var nextIncomplete = NextStepMap.TryGetValue(user.OnboardingStep, out var next) ? next : OnboardingStep.StoreProfile;

            if (desired <= user.OnboardingStep)
            {
                return desired;
            }

            return next;
        }

        private static OnboardingStep NormalizeStep(int? step) => step switch
        {
            1 => OnboardingStep.StoreProfile,
            2 => OnboardingStep.Verification,
            3 => OnboardingStep.Payout,
            _ => OnboardingStep.StoreProfile
        };

        private void PopulateInputFromUser(ApplicationUser user)
        {
            Input.StoreName = user.StoreName ?? string.Empty;
            Input.Description = user.StoreDescription ?? string.Empty;
            Input.ContactEmail = user.StoreContactEmail ?? user.Email ?? string.Empty;
            Input.ContactPhone = user.StoreContactPhone ?? user.PhoneNumber ?? string.Empty;
            Input.WebsiteUrl = user.StoreWebsiteUrl ?? string.Empty;
            Input.SellerType = user.SellerType == SellerType.Company || user.SellerType == SellerType.Individual ? user.SellerType : null;
            Input.CompanyName = user.CompanyName ?? string.Empty;
            Input.RegistrationNumber = user.VerificationRegistrationNumber ?? string.Empty;
            Input.TaxId = user.TaxId ?? string.Empty;
            Input.ContactPerson = user.VerificationContactPerson ?? string.Empty;
            Input.FullName = $"{user.FirstName} {user.LastName}".Trim();
            Input.PersonalIdNumber = user.VerificationPersonalIdNumber ?? string.Empty;
            Input.RegisteredAddress = user.VerificationAddress ?? string.Empty;
            Input.ContactPhoneVerification = user.PhoneNumber ?? string.Empty;
            Input.PayoutBeneficiaryName = user.PayoutBeneficiaryName ?? $"{user.FirstName} {user.LastName}".Trim();
            Input.PayoutAccountNumber = user.PayoutAccountNumber ?? string.Empty;
            Input.PayoutBankName = user.PayoutBankName ?? string.Empty;
        }

        private void ValidateStoreProfile()
        {
            var storeModel = new StoreStepInput
            {
                StoreName = Input.StoreName,
                Description = Input.Description,
                ContactEmail = Input.ContactEmail,
                ContactPhone = Input.ContactPhone,
                WebsiteUrl = Input.WebsiteUrl
            };

            AddValidationResults(ValidateModel(storeModel), "Input");
        }

        private void ValidateVerification()
        {
            var verificationModel = new VerificationStepInput
            {
                SellerType = Input.SellerType,
                CompanyName = Input.CompanyName,
                RegistrationNumber = Input.RegistrationNumber,
                TaxId = Input.TaxId,
                ContactPerson = Input.ContactPerson,
                FullName = Input.FullName,
                PersonalIdNumber = Input.PersonalIdNumber,
                RegisteredAddress = Input.RegisteredAddress,
                ContactPhone = Input.ContactPhoneVerification
            };

            AddValidationResults(ValidateModel(verificationModel), "Input");
        }

        private void ValidatePayout()
        {
            var payoutModel = new PayoutStepInput
            {
                PayoutBeneficiaryName = Input.PayoutBeneficiaryName,
                PayoutAccountNumber = Input.PayoutAccountNumber,
                PayoutBankName = Input.PayoutBankName
            };

            AddValidationResults(ValidateModel(payoutModel), "Input");
        }

        private static IList<ValidationResult> ValidateModel(object model)
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, context, results, validateAllProperties: true);
            return results;
        }

        private void AddValidationResults(IEnumerable<ValidationResult> results, string prefix)
        {
            foreach (var result in results)
            {
                var memberNames = result.MemberNames?.Any() == true ? result.MemberNames : new[] { string.Empty };
                foreach (var memberName in memberNames)
                {
                    var key = string.IsNullOrEmpty(memberName) ? prefix : $"{prefix}.{memberName}";
                    ModelState.AddModelError(key, result.ErrorMessage ?? "Invalid value.");
                }
            }
        }

        private class StoreStepInput
        {
            [Required]
            [StringLength(120, MinimumLength = 3)]
            public string StoreName { get; set; } = string.Empty;

            [StringLength(1000)]
            public string? Description { get; set; }

            [Required]
            [EmailAddress]
            [StringLength(320)]
            public string ContactEmail { get; set; } = string.Empty;

            [Phone]
            [StringLength(64)]
            public string? ContactPhone { get; set; }

            [Url]
            [StringLength(2048)]
            public string? WebsiteUrl { get; set; }
        }

        private class VerificationStepInput : IValidatableObject
        {
            [Required]
            public SellerType? SellerType { get; set; }

            [StringLength(200)]
            public string? CompanyName { get; set; }

            [StringLength(100)]
            [RegularExpression(@"^[A-Za-z0-9\-]{3,}$", ErrorMessage = "Registration number must be at least 3 characters and contain only letters, digits, or hyphens.")]
            public string? RegistrationNumber { get; set; }

            [StringLength(50)]
            [RegularExpression(@"^[A-Za-z0-9\-]{3,}$", ErrorMessage = "Tax ID must be at least 3 characters and contain only letters, digits, or hyphens.")]
            public string? TaxId { get; set; }

            [StringLength(150)]
            public string? ContactPerson { get; set; }

            [StringLength(200)]
            public string? FullName { get; set; }

            [StringLength(100)]
            [RegularExpression(@"^[A-Za-z0-9\-]{5,}$", ErrorMessage = "Personal ID number must be at least 5 characters and contain only letters, digits, or hyphens.")]
            public string? PersonalIdNumber { get; set; }

            [Required]
            [StringLength(300)]
            public string RegisteredAddress { get; set; } = string.Empty;

            [Required]
            [Phone]
            [StringLength(64)]
            public string ContactPhone { get; set; } = string.Empty;

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
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
                else
                {
                    yield return new ValidationResult("Select whether you operate as a company or an individual.", new[] { nameof(SellerType) });
                }
            }
        }

        private class PayoutStepInput
        {
            [Required]
            [StringLength(200, MinimumLength = 3)]
            public string PayoutBeneficiaryName { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 5)]
            [RegularExpression(@"^[A-Za-z0-9 \-]{5,}$", ErrorMessage = "Account number must contain at least 5 characters.")]
            public string PayoutAccountNumber { get; set; } = string.Empty;

            [StringLength(120)]
            public string? PayoutBankName { get; set; }
        }
    }
}
