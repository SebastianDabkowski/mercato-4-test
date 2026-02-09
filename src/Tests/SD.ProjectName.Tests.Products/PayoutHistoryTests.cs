using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Moq;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;
using SD.ProjectName.WebApp.Data;
using SD.ProjectName.WebApp.Pages.Seller.Payouts;

namespace SD.ProjectName.Tests.Products
{
    public class PayoutHistoryTests
    {
        [Fact]
        public async Task IndexModel_LoadsFilteredPayouts()
        {
            var user = new ApplicationUser
            {
                Id = "seller-1",
                AccountType = AccountType.Seller,
                FirstName = "Test",
                LastName = "Seller",
                OnboardingCompleted = true,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            var userManager = MockUserManager(user);
            PayoutScheduleQuery? capturedQuery = null;
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetPayoutSchedulesForSellerAsync(user.Id, It.IsAny<PayoutScheduleQuery>()))
                .Callback<string, PayoutScheduleQuery>((_, query) => capturedQuery = query)
                .ReturnsAsync(new PayoutScheduleResult
                {
                    Schedules =
                    [
                        new PayoutSchedule
                        {
                            Id = 5,
                            SellerId = user.Id,
                            Status = PayoutStatus.Paid,
                            TotalAmount = 125.50m,
                            ScheduledFor = new DateTimeOffset(2026, 02, 10, 0, 0, 0, TimeSpan.Zero),
                            ScheduledAt = new DateTimeOffset(2026, 02, 05, 12, 0, 0, TimeSpan.Zero),
                            PeriodStart = new DateTimeOffset(2026, 02, 01, 0, 0, 0, TimeSpan.Zero),
                            PeriodEnd = new DateTimeOffset(2026, 02, 09, 23, 59, 0, TimeSpan.Zero)
                        }
                    ],
                    Page = 1,
                    PageSize = 10,
                    TotalCount = 1
                });

            var model = new IndexModel(userManager.Object, repo.Object)
            {
                ScheduledFrom = new DateTime(2026, 2, 1),
                ScheduledTo = new DateTime(2026, 2, 10),
                Statuses = new List<string> { PayoutStatus.Paid },
                PageContext = BuildPageContext(user.Id)
            };

            var result = await model.OnGetAsync();

            Assert.IsType<PageResult>(result);
            Assert.Single(model.Payouts);
            Assert.Equal(PayoutStatus.Paid, model.Payouts[0].Status);
            Assert.NotNull(capturedQuery);
            Assert.Equal(1, capturedQuery!.Page);
            Assert.Contains(PayoutStatus.Paid, capturedQuery.Statuses);
            Assert.Equal(new DateTimeOffset(new DateTime(2026, 2, 1), TimeSpan.Zero), capturedQuery.ScheduledFrom);
            Assert.True(capturedQuery.ScheduledTo.HasValue);
        }

        [Fact]
        public async Task DetailsModel_LoadsBreakdownForSeller()
        {
            var user = new ApplicationUser
            {
                Id = "seller-2",
                AccountType = AccountType.Seller,
                FirstName = "Second",
                LastName = "Seller",
                OnboardingCompleted = true,
                TermsAcceptedAt = DateTimeOffset.UtcNow
            };

            var schedule = new PayoutSchedule
            {
                Id = 12,
                SellerId = user.Id,
                Status = PayoutStatus.Processing,
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),
                ScheduledFor = DateTimeOffset.UtcNow.AddDays(1),
                PeriodStart = DateTimeOffset.UtcNow.AddDays(-7),
                PeriodEnd = DateTimeOffset.UtcNow,
                TotalAmount = 200m,
                Items =
                [
                    new PayoutScheduleItem
                    {
                        EscrowLedgerEntryId = 2,
                        Amount = 150m,
                        EscrowEntry = new EscrowLedgerEntry
                        {
                            Id = 2,
                            OrderId = 44,
                            SellerOrderId = 55,
                            HeldAmount = 180m,
                            CommissionAmount = 30m,
                            SellerPayoutAmount = 150m,
                            PayoutEligibleAt = DateTimeOffset.UtcNow.AddDays(-2)
                        }
                    }
                ]
            };

            var userManager = MockUserManager(user);
            var repo = new Mock<ICartRepository>();
            repo.Setup(r => r.GetPayoutScheduleDetailsAsync(schedule.Id, user.Id))
                .ReturnsAsync(schedule);

            var model = new DetailsModel(userManager.Object, repo.Object)
            {
                PageContext = BuildPageContext(user.Id)
            };

            var result = await model.OnGetAsync(schedule.Id);

            Assert.IsType<PageResult>(result);
            Assert.NotNull(model.Schedule);
            Assert.Single(model.Breakdown);
            var item = model.Breakdown.First();
            Assert.Equal(schedule.Items[0].EscrowEntry!.SellerOrderId, item.SellerOrderId);
            Assert.Equal(schedule.Items[0].EscrowEntry!.CommissionAmount, item.CommissionAmount);
        }

        private static PageContext BuildPageContext(string userId)
        {
            return new PageContext(new ActionContext(
                new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)]))
                },
                new RouteData(),
                new PageActionDescriptor()));
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
