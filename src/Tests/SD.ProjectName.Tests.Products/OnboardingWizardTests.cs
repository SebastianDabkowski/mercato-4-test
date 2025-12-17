using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Pages.Seller;

namespace SD.ProjectName.Tests.Products
{
    public class OnboardingWizardTests
    {
        [Fact]
        public async Task Onboarding_ShouldAdvanceToVerificationAfterStoreStep()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            var dbContext = scopedProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();

            var userManager = scopedProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var seller = new ApplicationUser
            {
                Id = "seller-1",
                UserName = "seller@example.com",
                Email = "seller@example.com",
                FirstName = "Seller",
                LastName = "One",
                AccountType = AccountType.Seller,
                TermsAcceptedAt = DateTimeOffset.UtcNow,
                RequiresKyc = true,
                KycStatus = KycStatus.NotStarted
            };

            await userManager.CreateAsync(seller);

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, seller.Id),
                    new Claim(ClaimTypes.Name, seller.UserName!)
                }, "TestAuth"))
            };

            var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());

            var page = new OnboardingModel(userManager, NullLogger<OnboardingModel>.Instance)
            {
                PageContext = new PageContext(actionContext),
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
                Input = new OnboardingModel.InputModel
                {
                    Step = 1,
                    StoreName = "My Test Store",
                    ContactEmail = "contact@example.com",
                    Description = "Short description"
                }
            };

            var result = await page.OnPostAsync();

            Assert.IsType<RedirectToPageResult>(result);
            var redirect = (RedirectToPageResult)result;
            Assert.Equal(2, redirect.RouteValues?["step"]);

            var updated = await userManager.FindByIdAsync(seller.Id);
            Assert.NotNull(updated);
            Assert.Equal(OnboardingStep.StoreProfile, updated!.OnboardingStep);
            Assert.Equal("My Test Store", updated.StoreName);
            Assert.Equal("contact@example.com", updated.StoreContactEmail);
        }

        [Fact]
        public async Task Onboarding_ShouldResumeFromPayoutWhenVerificationCompleted()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            var dbContext = scopedProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();

            var userManager = scopedProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var seller = new ApplicationUser
            {
                Id = "seller-2",
                UserName = "seller2@example.com",
                Email = "seller2@example.com",
                FirstName = "Seller",
                LastName = "Two",
                AccountType = AccountType.Seller,
                TermsAcceptedAt = DateTimeOffset.UtcNow,
                RequiresKyc = true,
                KycStatus = KycStatus.NotStarted,
                OnboardingStep = OnboardingStep.Verification,
                StoreName = "Existing Store",
                StoreContactEmail = "store@example.com"
            };

            await userManager.CreateAsync(seller);

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, seller.Id),
                    new Claim(ClaimTypes.Name, seller.UserName!)
                }, "TestAuth"))
            };

            var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());

            var page = new OnboardingModel(userManager, NullLogger<OnboardingModel>.Instance)
            {
                PageContext = new PageContext(actionContext),
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };

            var result = await page.OnGetAsync();

            Assert.IsType<PageResult>(result);
            Assert.Equal(3, page.Step);
            Assert.Equal("Existing Store", page.Input.StoreName);
            Assert.Equal("store@example.com", page.Input.ContactEmail);
        }
    }
}
