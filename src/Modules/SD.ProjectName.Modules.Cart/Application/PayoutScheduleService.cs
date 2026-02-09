using System;
using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class PayoutScheduleService
{
    private readonly ICartRepository _cartRepository;
    private readonly PayoutScheduleOptions _options;
    private readonly TimeProvider _timeProvider;

    public PayoutScheduleService(
        ICartRepository cartRepository,
        IOptions<PayoutScheduleOptions> options,
        TimeProvider timeProvider)
    {
        _cartRepository = cartRepository;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<List<PayoutSchedule>> ScheduleEligiblePayoutsAsync(DateTimeOffset? nowOverride = null)
    {
        var now = nowOverride ?? _timeProvider.GetUtcNow();
        var eligible = await _cartRepository.GetPayoutEligibleEscrowEntriesAsync(now);
        var created = new List<PayoutSchedule>();

        foreach (var group in eligible.GroupBy(e => e.SellerId))
        {
            var total = group.Sum(e => e.SellerPayoutAmount);
            if (total < _options.MinimumPayoutAmount)
            {
                continue;
            }

            var schedule = new PayoutSchedule
            {
                SellerId = group.Key,
                PeriodStart = group.Min(e => e.PayoutEligibleAt),
                PeriodEnd = now,
                ScheduledAt = now,
                ScheduledFor = CalculateScheduledFor(now),
                Status = PayoutStatus.Scheduled,
                TotalAmount = Math.Round(total, 2, MidpointRounding.AwayFromZero),
                AttemptCount = 0,
                Items = group.Select(e => new PayoutScheduleItem
                {
                    EscrowLedgerEntryId = e.Id,
                    Amount = Math.Round(e.SellerPayoutAmount, 2, MidpointRounding.AwayFromZero)
                }).ToList()
            };

            await _cartRepository.AddPayoutScheduleAsync(schedule);
            created.Add(schedule);
        }

        return created;
    }

    public async Task<PayoutSchedule?> MarkProcessingAsync(int scheduleId)
    {
        var schedule = await _cartRepository.GetPayoutScheduleAsync(scheduleId);
        if (schedule is null)
        {
            return null;
        }

        schedule.Status = PayoutStatus.Processing;
        schedule.ProcessingStartedAt ??= _timeProvider.GetUtcNow();
        schedule.AttemptCount = schedule.AttemptCount == 0 ? 1 : schedule.AttemptCount + 1;
        schedule.ErrorReference = null;

        await _cartRepository.SaveChangesAsync();
        return schedule;
    }

    public async Task<PayoutSchedule?> MarkPaidAsync(int scheduleId)
    {
        var schedule = await _cartRepository.GetPayoutScheduleWithItemsAsync(scheduleId);
        if (schedule is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        schedule.Status = PayoutStatus.Paid;
        schedule.PaidAt = now;
        schedule.ErrorReference = null;

        foreach (var item in schedule.Items)
        {
            if (item.EscrowEntry is null)
            {
                continue;
            }

            item.EscrowEntry.Status = EscrowLedgerStatus.ReleasedToSeller;
            item.EscrowEntry.ReleasedAt = now;
            item.EscrowEntry.ReleaseReason = "Payout released to seller";
        }

        await _cartRepository.SaveChangesAsync();
        return schedule;
    }

    public async Task<PayoutSchedule?> MarkFailedAsync(int scheduleId, string errorReference)
    {
        var schedule = await _cartRepository.GetPayoutScheduleAsync(scheduleId);
        if (schedule is null)
        {
            return null;
        }

        schedule.Status = PayoutStatus.Failed;
        schedule.ErrorReference = errorReference;
        schedule.AttemptCount = schedule.AttemptCount == 0 ? 1 : schedule.AttemptCount;

        await _cartRepository.SaveChangesAsync();
        return schedule;
    }

    public async Task<PayoutSchedule?> RetryAsync(int scheduleId)
    {
        var schedule = await _cartRepository.GetPayoutScheduleAsync(scheduleId);
        if (schedule is null || schedule.Status != PayoutStatus.Failed)
        {
            return schedule;
        }

        schedule.Status = PayoutStatus.Scheduled;
        schedule.ErrorReference = null;
        schedule.ScheduledAt = _timeProvider.GetUtcNow();

        await _cartRepository.SaveChangesAsync();
        return schedule;
    }

    public async Task<List<PayoutSchedule>> GetRecentSchedulesForSellerAsync(string sellerId, int take = 5)
    {
        return await _cartRepository.GetPayoutSchedulesForSellerAsync(sellerId, take);
    }

    private DateTimeOffset CalculateScheduledFor(DateTimeOffset now)
    {
        var intervalDays = _options.ScheduleIntervalDays <= 0 ? 7 : _options.ScheduleIntervalDays;
        return now.Date.AddDays(intervalDays);
    }
}
