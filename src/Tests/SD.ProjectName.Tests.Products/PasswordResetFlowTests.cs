using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SD.ProjectName.WebApp.Areas.Identity.Pages.Account;
using SD.ProjectName.WebApp.Data;
using Xunit;

namespace SD.ProjectName.Tests.Products
{
    public class PasswordResetFlowTests
    {
        [Fact]
        public async Task ForgotPassword_DoesNotRevealUnknownEmail()
        {
            var userManager = MockUserManager();
            var emailSender = new Mock<IEmailSender>();
            var model = new ForgotPasswordModel(userManager.Object, emailSender.Object, NullLogger<ForgotPasswordModel>.Instance)
            {
                PageContext = CreatePageContext()
            };
            model.Input.Email = "missing@example.com";

            var result = await model.OnPostAsync();

            Assert.True(model.EmailSent);
            Assert.IsType<PageResult>(result);
            emailSender.Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ResetPassword_InvalidToken_ShowsInvalidLink()
        {
            var user = new ApplicationUser
            {
                Id = "user-1",
                Email = "user@example.com",
                UserName = "user@example.com",
                FirstName = "Test",
                LastName = "User",
                AccountType = AccountType.Buyer,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            var userManager = MockUserManager();
            userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(m => m.ResetPasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "Invalid token" }));

            var signInManager = BuildSignInManager(userManager.Object);
            var model = new ResetPasswordModel(userManager.Object, signInManager, NullLogger<ResetPasswordModel>.Instance)
            {
                PageContext = CreatePageContext(),
                Input = new ResetPasswordModel.InputModel
                {
                    UserId = user.Id,
                    Code = "bad-token",
                    Password = "S3cure!Passw0rd",
                    ConfirmPassword = "S3cure!Passw0rd"
                }
            };

            var result = await model.OnPostAsync();

            Assert.IsType<PageResult>(result);
            Assert.True(model.ShowInvalidLink);
        }

        private static PageContext CreatePageContext()
        {
            var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
            return new PageContext(actionContext);
        }

        private static Mock<UserManager<ApplicationUser>> MockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var options = new Mock<IOptions<IdentityOptions>>();
            options.Setup(o => o.Value).Returns(new IdentityOptions());
            var passwordHasher = new Mock<IPasswordHasher<ApplicationUser>>();
            var userValidators = Array.Empty<IUserValidator<ApplicationUser>>();
            var passwordValidators = Array.Empty<IPasswordValidator<ApplicationUser>>();
            var lookupNormalizer = new Mock<ILookupNormalizer>();
            var identityErrorDescriber = new IdentityErrorDescriber();
            var serviceProvider = new Mock<IServiceProvider>();
            var logger = new Mock<ILogger<UserManager<ApplicationUser>>>();

            return new Mock<UserManager<ApplicationUser>>(
                store.Object,
                options.Object,
                passwordHasher.Object,
                userValidators,
                passwordValidators,
                lookupNormalizer.Object,
                identityErrorDescriber,
                serviceProvider.Object,
                logger.Object);
        }

        private static SignInManager<ApplicationUser> BuildSignInManager(UserManager<ApplicationUser> userManager)
        {
            var contextAccessor = new Mock<IHttpContextAccessor>();
            contextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            var options = new Mock<IOptions<IdentityOptions>>();
            options.Setup(o => o.Value).Returns(new IdentityOptions());
            var logger = new Mock<ILogger<SignInManager<ApplicationUser>>>();
            var schemes = new Mock<IAuthenticationSchemeProvider>();
            var confirmation = new Mock<IUserConfirmation<ApplicationUser>>();

            return new SignInManager<ApplicationUser>(userManager, contextAccessor.Object, claimsFactory.Object, options.Object, logger.Object, schemes.Object, confirmation.Object);
        }
    }
}
