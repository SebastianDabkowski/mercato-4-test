using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class EscrowService
{
    private readonly ICartRepository _cartRepository;
    private readonly TimeProvider _timeProvider;
    private readonly CommissionService _commissionService;
    private readonly EscrowOptions _options;

    public EscrowService(
        ICartRepository cartRepository,
        TimeProvider timeProvider,
        CommissionService commissionService,
        IOptions<EscrowOptions> options)
    {
        _cartRepository = cartRepository;
        _timeProvider = timeProvider;
        _commissionService = commissionService;
        _options = options.Value;
    }

    public async Task EnsureEscrowAsync(OrderModel? order)
    {
        if (order is null || order.SubOrders.Count == 0)
        {
            return;
        }

        if (await _cartRepository.HasEscrowEntriesAsync(order.Id))
        {
            return;
        }

        _commissionService.EnsureCommissionCalculated(order);
        var createdAt = order.CreatedAt == default ? _timeProvider.GetUtcNow() : order.CreatedAt;
        var payoutEligibleAt = createdAt.AddDays(_options.PayoutEligibilityDelayDays);
        var entries = new List<EscrowLedgerEntry>();

        foreach (var subOrder in order.SubOrders)
        {
            var heldAmount = Math.Max(0m, subOrder.TotalAmount - subOrder.RefundedAmount);
            var commission = Math.Round(subOrder.CommissionAmount, 6, MidpointRounding.AwayFromZero);
            var sellerPayout = Math.Max(0m, heldAmount - commission);

            entries.Add(new EscrowLedgerEntry
            {
                OrderId = order.Id,
                SellerOrderId = subOrder.Id,
                BuyerId = order.BuyerId,
                SellerId = subOrder.SellerId,
                HeldAmount = heldAmount,
                CommissionAmount = commission,
                SellerPayoutAmount = sellerPayout,
                Status = EscrowLedgerStatus.Held,
                CreatedAt = createdAt,
                PayoutEligibleAt = payoutEligibleAt
            });
        }

        if (entries.Count == 0)
        {
            return;
        }

        await _cartRepository.AddEscrowEntriesAsync(entries);
    }

    public async Task ReleaseEscrowToBuyerAsync(OrderModel? order, string reason)
    {
        if (order is null)
        {
            return;
        }

        var entries = await _cartRepository.GetEscrowEntriesForOrderAsync(order.Id);
        if (entries.Count == 0)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var updated = false;

        foreach (var entry in entries.Where(e => e.Status == EscrowLedgerStatus.Held))
        {
            entry.Status = EscrowLedgerStatus.ReleasedToBuyer;
            entry.ReleasedAt = now;
            entry.ReleaseReason = reason;
            updated = true;
        }

        if (updated)
        {
            await _cartRepository.SaveChangesAsync();
        }
    }

    public async Task ReleaseSellerOrderEscrowToBuyerAsync(int sellerOrderId, string reason)
    {
        var entry = await _cartRepository.GetEscrowEntryForSellerOrderAsync(sellerOrderId);
        if (entry is null ||
            entry.Status == EscrowLedgerStatus.ReleasedToBuyer ||
            entry.Status == EscrowLedgerStatus.ReleasedToSeller)
        {
            return;
        }

        entry.Status = EscrowLedgerStatus.ReleasedToBuyer;
        entry.ReleasedAt = _timeProvider.GetUtcNow();
        entry.ReleaseReason = reason;
        await _cartRepository.SaveChangesAsync();
    }

    public async Task UpdateEscrowForSellerOrderAsync(SellerOrderModel sellerOrder)
    {
        var entry = await _cartRepository.GetEscrowEntryForSellerOrderAsync(sellerOrder.Id);
        if (entry is null)
        {
            return;
        }

        var heldAmount = Math.Max(0m, sellerOrder.TotalAmount - sellerOrder.RefundedAmount);
        var commission = Math.Round(sellerOrder.CommissionAmount, 6, MidpointRounding.AwayFromZero);

        entry.HeldAmount = heldAmount;
        entry.CommissionAmount = commission;
        entry.SellerPayoutAmount = Math.Max(0m, heldAmount - commission);
        await _cartRepository.SaveChangesAsync();
    }
}
