using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SD.ProjectName.WebApp.Areas.Identity.Pages.Account;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Identity;

namespace SD.ProjectName.Tests.Products
{
    public class IdentityRegistrationTests
    {
        [Fact]
        public async Task CommonPasswordValidator_ShouldRejectCommonPassword()
        {
            var validator = new CommonPasswordValidator(new NullLogger<CommonPasswordValidator>());
            var userManager = MockUserManager();
            var user = new ApplicationUser
            {
                Email = "user@example.com",
                UserName = "user@example.com",
                FirstName = "Test",
                LastName = "User",
                AccountType = AccountType.Buyer,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            var result = await validator.ValidateAsync(userManager, user, "password");

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.Code == "CommonPassword");
        }

        [Fact]
        public async Task CommonPasswordValidator_ShouldAllowStrongPassword()
        {
            var validator = new CommonPasswordValidator(new NullLogger<CommonPasswordValidator>());
            var userManager = MockUserManager();
            var user = new ApplicationUser
            {
                Email = "user@example.com",
                UserName = "user@example.com",
                FirstName = "Test",
                LastName = "User",
                AccountType = AccountType.Buyer,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            var result = await validator.ValidateAsync(userManager, user, "S3cure!Passw0rd");

            Assert.True(result.Succeeded);
        }

        [Fact]
        public void InputModel_ShouldRequireSellerFields()
        {
            var input = new RegisterModel.InputModel
            {
                AccountType = AccountType.Seller,
                Email = "seller@example.com",
                Password = "S3cure!Passw0rd",
                ConfirmPassword = "S3cure!Passw0rd",
                FirstName = "Sally",
                LastName = "Seller",
                AcceptTerms = true
            };

            var validationResults = Validate(input);

            Assert.Contains(validationResults, r => r.ErrorMessage == "Company name is required for sellers.");
            Assert.Contains(validationResults, r => r.ErrorMessage == "Tax ID is required for sellers.");
        }

        [Fact]
        public void InputModel_ShouldRequireTermsAcceptance()
        {
            var input = new RegisterModel.InputModel
            {
                AccountType = AccountType.Buyer,
                Email = "buyer@example.com",
                Password = "S3cure!Passw0rd",
                ConfirmPassword = "S3cure!Passw0rd",
                FirstName = "Betty",
                LastName = "Buyer",
                AcceptTerms = false
            };

            var validationResults = Validate(input);

            Assert.Contains(validationResults, r => r.ErrorMessage == "You must accept the terms to continue.");
        }

        [Fact]
        public void ApplicationUser_DefaultStatusIsUnverified()
        {
            var user = new ApplicationUser
            {
                FirstName = "Test",
                LastName = "User",
                AccountType = AccountType.Buyer,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            Assert.Equal(AccountStatus.Unverified, user.AccountStatus);
        }

        [Fact]
        public void ApplicationUser_DefaultsTwoFactorMethodToNone()
        {
            var user = new ApplicationUser
            {
                FirstName = "Test",
                LastName = "User",
                AccountType = AccountType.Buyer,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            Assert.Equal(TwoFactorMethod.None, user.TwoFactorMethod);
        }

        private static IList<ValidationResult> Validate(object model)
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, context, results, validateAllProperties: true);
            return results;
        }

        private static UserManager<ApplicationUser> MockUserManager()
        {
            var store = new Mock<IUserPasswordStore<ApplicationUser>>(MockBehavior.Loose);
            return new UserManager<ApplicationUser>(
                store.Object,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!);
        }
    }
}
