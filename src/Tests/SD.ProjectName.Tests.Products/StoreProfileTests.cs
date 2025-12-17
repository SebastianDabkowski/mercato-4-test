using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
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
    public class StoreProfileTests
    {
        [Fact]
        public void InputModel_ShouldRequireStoreNameAndContactEmail()
        {
            var input = new StoreModel.InputModel();

            var results = Validate(input);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(StoreModel.InputModel.StoreName)));
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(StoreModel.InputModel.ContactEmail)));
        }

        [Fact]
        public void InputModel_ShouldValidateWebsiteUrl()
        {
            var input = new StoreModel.InputModel
            {
                StoreName = "Test Store",
                ContactEmail = "store@example.com",
                WebsiteUrl = "not-a-url"
            };

            var results = Validate(input);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(StoreModel.InputModel.WebsiteUrl)));
        }

        [Fact]
        public async Task OnPost_ShouldRejectDuplicateStoreName()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connection));
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

            var existingSeller = new ApplicationUser
            {
                Id = "existing",
                UserName = "existing@example.com",
                Email = "existing@example.com",
                FirstName = "Existing",
                LastName = "Seller",
                AccountType = AccountType.Seller,
                StoreName = "My Demo Store",
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            var currentSeller = new ApplicationUser
            {
                Id = "current",
                UserName = "current@example.com",
                Email = "current@example.com",
                FirstName = "Current",
                LastName = "Seller",
                AccountType = AccountType.Seller,
                KycStatus = KycStatus.Approved,
                RequiresKyc = false,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            await userManager.CreateAsync(existingSeller);
            await userManager.CreateAsync(currentSeller);

            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.WebRootPath).Returns(Path.GetTempPath());
            environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, currentSeller.Id),
                new Claim(ClaimTypes.Name, currentSeller.UserName!)
            }, "TestAuth"));

            var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());

            var page = new StoreModel(userManager, environment.Object, NullLogger<StoreModel>.Instance)
            {
                PageContext = new PageContext(actionContext),
                TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
                Url = new UrlHelper(actionContext),
                Input = new StoreModel.InputModel
                {
                    StoreName = "My Demo Store",
                    ContactEmail = "seller@example.com"
                }
            };

            var result = await page.OnPostAsync();

            Assert.IsType<PageResult>(result);
            Assert.True(page.ModelState.ContainsKey("Input.StoreName"));
            var storeNameState = page.ModelState["Input.StoreName"];
            Assert.NotNull(storeNameState);
            Assert.NotEmpty(storeNameState.Errors);
        }

        private static IList<ValidationResult> Validate(object model)
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, context, results, validateAllProperties: true);
            return results;
        }
    }
}
