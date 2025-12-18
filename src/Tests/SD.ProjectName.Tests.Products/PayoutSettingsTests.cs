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
    public class PayoutSettingsTests
    {
        [Fact]
        public async Task OnGet_ShouldRedirectToOnboarding_WhenNotCompleted()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>()
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
                Id = "seller-3",
                UserName = "payout@example.com",
                Email = "payout@example.com",
                FirstName = "Seller",
                LastName = "Three",
                AccountType = AccountType.Seller,
                OnboardingCompleted = false,
                RequiresKyc = true,
                TermsAcceptedAt = DateTimeOffset.UtcNow
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

            var page = new PayoutModel(userManager, NullLogger<PayoutModel>.Instance)
            {
                PageContext = new PageContext(actionContext),
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
            };

            var result = await page.OnGetAsync();

            var redirect = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("/Seller/Onboarding", redirect.PageName);
        }

        [Fact]
        public async Task OnPost_ShouldUpdateBankDetailsAndDefaultMethod()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>()
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
                Id = "seller-4",
                UserName = "updatepayout@example.com",
                Email = "updatepayout@example.com",
                FirstName = "Alex",
                LastName = "Doe",
                AccountType = AccountType.Seller,
                OnboardingCompleted = true,
                RequiresKyc = true,
                KycStatus = KycStatus.Approved,
                TermsAcceptedAt = DateTimeOffset.UtcNow,
                PayoutBeneficiaryName = "Old Beneficiary",
                PayoutAccountNumber = "OLD-12345",
                PayoutBankName = "Old Bank",
                PayoutDefaultMethod = PayoutMethod.BankTransfer
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

            var page = new PayoutModel(userManager, NullLogger<PayoutModel>.Instance)
            {
                PageContext = new PageContext(actionContext),
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
                Input = new PayoutModel.InputModel
                {
                    PayoutBeneficiaryName = "New Beneficiary",
                    PayoutAccountNumber = "PL445566778899",
                    PayoutBankName = "Updated Bank",
                    PreferredMethod = PayoutMethod.BankTransfer
                }
            };

            var result = await page.OnPostAsync();

            Assert.IsType<RedirectToPageResult>(result);

            var updated = await userManager.FindByIdAsync(seller.Id);
            Assert.NotNull(updated);
            Assert.Equal("New Beneficiary", updated!.PayoutBeneficiaryName);
            Assert.Equal("PL445566778899", updated.PayoutAccountNumber);
            Assert.Equal("Updated Bank", updated.PayoutBankName);
            Assert.Equal(PayoutMethod.BankTransfer, updated.PayoutDefaultMethod);
            Assert.Equal(KycStatus.Pending, updated.KycStatus);
            Assert.True(updated.RequiresKyc);
        }
    }
}
