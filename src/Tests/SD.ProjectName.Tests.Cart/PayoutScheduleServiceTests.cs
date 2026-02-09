using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Infrastructure;

namespace SD.ProjectName.Tests.Cart;

public class PayoutScheduleServiceTests
{
    [Fact]
    public async Task ScheduleEligiblePayoutsAsync_AggregatesBySellerAndSkipsBelowThreshold()
    {
        var now = new DateTimeOffset(2026, 2, 9, 12, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(nameof(ScheduleEligiblePayoutsAsync_AggregatesBySellerAndSkipsBelowThreshold))
            .Options;
        var context = new CartDbContext(options);
        var repo = new CartRepository(context);
        var primaryEntry = new EscrowLedgerEntry
        {
            SellerId = "seller-1",
            BuyerId = "buyer",
            SellerOrderId = 11,
            OrderId = 1,
            Status = EscrowLedgerStatus.Held,
            HeldAmount = 150m,
            CommissionAmount = 0m,
            SellerPayoutAmount = 120m,
            PayoutEligibleAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-2)
        };
        var secondaryEntry = new EscrowLedgerEntry
        {
            SellerId = "seller-1",
            BuyerId = "buyer",
            SellerOrderId = 12,
            OrderId = 1,
            Status = EscrowLedgerStatus.Held,
            HeldAmount = 30m,
            CommissionAmount = 0m,
            SellerPayoutAmount = 30m,
            PayoutEligibleAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-2)
        };
        var belowThresholdEntry = new EscrowLedgerEntry
        {
            SellerId = "seller-2",
            BuyerId = "buyer",
            SellerOrderId = 13,
            OrderId = 1,
            Status = EscrowLedgerStatus.Held,
            HeldAmount = 20m,
            CommissionAmount = 0m,
            SellerPayoutAmount = 20m,
            PayoutEligibleAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-2)
        };
        context.EscrowLedgerEntries.AddRange(primaryEntry, secondaryEntry, belowThresholdEntry);
        await context.SaveChangesAsync();

        var service = new PayoutScheduleService(
            repo,
            Options.Create(new PayoutScheduleOptions { MinimumPayoutAmount = 100m, ScheduleIntervalDays = 7 }),
            new FixedTimeProvider(now));

        var created = await service.ScheduleEligiblePayoutsAsync(now);

        Assert.Single(created);
        var schedule = created.Single();
        Assert.Equal("seller-1", schedule.SellerId);
        Assert.Equal(150m, schedule.TotalAmount);
        Assert.Equal(2, schedule.Items.Count);
        Assert.Equal(now.Date.AddDays(7), schedule.ScheduledFor.Date);
        Assert.Equal(1, context.PayoutSchedules.Count());
        Assert.Equal(2, context.PayoutScheduleItems.Count());
        Assert.DoesNotContain(context.PayoutScheduleItems, i => i.EscrowLedgerEntryId == belowThresholdEntry.Id);
    }

    [Fact]
    public async Task MarkPaidAsync_ReleasesEscrowEntries()
    {
        var now = new DateTimeOffset(2026, 2, 9, 12, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(nameof(MarkPaidAsync_ReleasesEscrowEntries))
            .Options;
        var context = new CartDbContext(options);
        var repo = new CartRepository(context);
        var entry = new EscrowLedgerEntry
        {
            SellerId = "seller-1",
            BuyerId = "buyer",
            SellerOrderId = 21,
            OrderId = 2,
            Status = EscrowLedgerStatus.Held,
            HeldAmount = 50m,
            CommissionAmount = 0m,
            SellerPayoutAmount = 50m,
            PayoutEligibleAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-2)
        };
        context.EscrowLedgerEntries.Add(entry);
        await context.SaveChangesAsync();

        var schedule = new PayoutSchedule
        {
            SellerId = "seller-1",
            PeriodStart = now.AddDays(-7),
            PeriodEnd = now,
            ScheduledAt = now,
            ScheduledFor = now.AddDays(7),
            Status = PayoutStatus.Processing,
            TotalAmount = 50m,
            Items = new List<PayoutScheduleItem>
            {
                new()
                {
                    EscrowLedgerEntryId = entry.Id,
                    Amount = 50m
                }
            }
        };
        context.PayoutSchedules.Add(schedule);
        await context.SaveChangesAsync();

        var service = new PayoutScheduleService(
            repo,
            Options.Create(new PayoutScheduleOptions()),
            new FixedTimeProvider(now));

        var result = await service.MarkPaidAsync(schedule.Id);

        Assert.NotNull(result);
        Assert.Equal(PayoutStatus.Paid, result!.Status);
        var refreshedEntry = await context.EscrowLedgerEntries.FindAsync(entry.Id);
        Assert.NotNull(refreshedEntry!.ReleasedAt);
        Assert.Equal(EscrowLedgerStatus.ReleasedToSeller, refreshedEntry.Status);
        Assert.Equal("Payout released to seller", refreshedEntry.ReleaseReason);
    }

    [Fact]
    public async Task MarkFailedAndRetryAsync_StoresErrorReference()
    {
        var now = new DateTimeOffset(2026, 2, 9, 12, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(nameof(MarkFailedAndRetryAsync_StoresErrorReference))
            .Options;
        var context = new CartDbContext(options);
        var repo = new CartRepository(context);
        var schedule = new PayoutSchedule
        {
            SellerId = "seller-3",
            PeriodStart = now.AddDays(-7),
            PeriodEnd = now,
            ScheduledAt = now.AddDays(-1),
            ScheduledFor = now.AddDays(6),
            Status = PayoutStatus.Scheduled,
            TotalAmount = 75m
        };
        context.PayoutSchedules.Add(schedule);
        await context.SaveChangesAsync();

        var service = new PayoutScheduleService(
            repo,
            Options.Create(new PayoutScheduleOptions()),
            new FixedTimeProvider(now));

        var failed = await service.MarkFailedAsync(schedule.Id, "ERR-123");
        Assert.NotNull(failed);
        Assert.Equal(PayoutStatus.Failed, failed!.Status);
        Assert.Equal("ERR-123", failed.ErrorReference);
        Assert.Equal(1, failed.AttemptCount);

        var retried = await service.RetryAsync(schedule.Id);
        Assert.NotNull(retried);
        Assert.Equal(PayoutStatus.Scheduled, retried!.Status);
        Assert.Null(retried.ErrorReference);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
