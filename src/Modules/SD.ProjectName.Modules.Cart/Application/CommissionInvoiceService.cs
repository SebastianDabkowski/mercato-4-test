using Microsoft.Extensions.Options;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Modules.Cart.Application;

public class CommissionInvoiceService
{
    private readonly ICartRepository _cartRepository;
    private readonly CommissionInvoiceOptions _options;
    private readonly TimeProvider _timeProvider;

    public CommissionInvoiceService(
        ICartRepository cartRepository,
        IOptions<CommissionInvoiceOptions> options,
        TimeProvider timeProvider)
    {
        _cartRepository = cartRepository;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<CommissionInvoice?> EnsurePreviousMonthInvoiceAsync(string sellerId, string sellerName, DateTimeOffset? nowOverride = null)
    {
        var now = nowOverride ?? _timeProvider.GetUtcNow();
        var currentMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodStart = currentMonthStart.AddMonths(-1);
        var periodEnd = currentMonthStart.AddTicks(-1);

        var existing = await _cartRepository.GetCommissionInvoiceForPeriodAsync(sellerId, periodStart, periodEnd);
        if (existing is not null)
        {
            return existing;
        }

        var baseEntries = await _cartRepository.GetCommissionableEscrowEntriesAsync(sellerId, periodStart, periodEnd);
        var correctionEntries = await _cartRepository.GetCommissionCorrectionsAsync(sellerId, periodStart, periodEnd);

        var lines = new List<CommissionInvoiceLine>();
        foreach (var entry in baseEntries)
        {
            var amount = Math.Round(entry.CommissionAmount, 2, MidpointRounding.AwayFromZero);
            if (amount == 0m)
            {
                continue;
            }

            lines.Add(new CommissionInvoiceLine
            {
                EscrowLedgerEntryId = entry.Id,
                Amount = amount,
                Description = $"Order #{entry.OrderId} – Seller order #{entry.SellerOrderId}"
            });
        }

        foreach (var entry in correctionEntries)
        {
            var amount = Math.Round(Math.Abs(entry.CommissionAmount), 2, MidpointRounding.AwayFromZero);
            if (amount == 0m)
            {
                continue;
            }

            lines.Add(new CommissionInvoiceLine
            {
                EscrowLedgerEntryId = entry.Id,
                Amount = amount * -1,
                Description = $"Correction for order #{entry.OrderId}",
                IsCorrection = true
            });
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var taxRate = ResolveTaxRate(sellerId);
        var subtotal = lines.Sum(l => l.Amount);
        var taxAmount = Math.Round(subtotal * taxRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + taxAmount;
        var invoice = new CommissionInvoice
        {
            SellerId = sellerId,
            SellerName = sellerName,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            IssuedAt = now,
            Status = CommissionInvoiceStatus.Issued,
            TaxRate = taxRate,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            TotalAmount = total,
            Currency = string.IsNullOrWhiteSpace(_options.Currency) ? "PLN" : _options.Currency,
            IsCreditNote = total < 0,
            Lines = lines
        };

        await _cartRepository.AddCommissionInvoiceAsync(invoice);
        invoice.Number = BuildInvoiceNumber(invoice.Id, now);
        await _cartRepository.SaveChangesAsync();

        return invoice;
    }

    public Task<CommissionInvoice?> GetInvoiceAsync(int invoiceId, string sellerId) =>
        _cartRepository.GetCommissionInvoiceAsync(invoiceId, sellerId);

    public Task<CommissionInvoiceResult> GetInvoicesAsync(string sellerId, CommissionInvoiceQuery query) =>
        _cartRepository.GetCommissionInvoicesAsync(sellerId, query);

    private decimal ResolveTaxRate(string sellerId)
    {
        if (!string.IsNullOrWhiteSpace(sellerId) &&
            _options.SellerTaxOverrides.TryGetValue(sellerId, out var rate))
        {
            return rate;
        }

        return _options.DefaultTaxRate;
    }

    private string BuildInvoiceNumber(int invoiceId, DateTimeOffset issuedAt)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.NumberPrefix) ? "CI" : _options.NumberPrefix.Trim();
        return $"{prefix}-{issuedAt:yyyy}-{invoiceId:D5}";
    }
}
