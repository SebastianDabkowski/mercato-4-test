using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using SD.ProjectName.WebApp;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Pages.Seller;

namespace SD.ProjectName.Tests.Products
{
    public class SellerInternalUserTests
    {
        [Fact]
        public void ApplicationUser_DefaultSellerRoleIsStoreOwner()
        {
            var user = new ApplicationUser
            {
                FirstName = "Test",
                LastName = "User",
                AccountType = AccountType.Seller,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            Assert.Equal(SellerTeamRole.StoreOwner, user.SellerRole);
        }

        [Fact]
        public async Task DashboardModel_ShouldExposeFeatureFlag()
        {
            var user = new ApplicationUser
            {
                Id = "owner-1",
                FirstName = "Owner",
                LastName = "User",
                AccountType = AccountType.Seller,
                TermsAcceptedAt = DateTimeOffset.UtcNow,
                OnboardingCompleted = true,
                RequiresKyc = false,
                KycStatus = KycStatus.Approved
            };

            var userManager = MockUserManager(user);
            var featureOptions = Options.Create(new FeatureFlags { EnableSellerInternalUsers = true });
            var model = new DashboardModel(userManager.Object, featureOptions)
            {
                PageContext = new PageContext(new ActionContext(
                    new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]))
                    },
                    new RouteData(),
                    new PageActionDescriptor()))
            };

            var result = await model.OnGetAsync();

            Assert.IsType<PageResult>(result);
            Assert.True(model.SellerUserManagementEnabled);
        }

        private static Mock<UserManager<ApplicationUser>> MockUserManager(ApplicationUser returnUser)
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var mgr = new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            mgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(returnUser);
            return mgr;
        }
    }
}
