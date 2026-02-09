using Microsoft.Extensions.Options;
using Moq;
using SD.ProjectName.Modules.Cart.Application;
using SD.ProjectName.Modules.Cart.Domain;
using SD.ProjectName.Modules.Cart.Domain.Interfaces;

namespace SD.ProjectName.Tests.Cart;

public class CommissionInvoiceServiceTests
{
    [Fact]
    public async Task EnsurePreviousMonthInvoiceAsync_CreatesInvoiceWithNumberAndTotals()
    {
        var sellerId = "seller-1";
        var sellerName = "Seller One";
        var now = new DateTimeOffset(2026, 2, 9, 10, 0, 0, TimeSpan.Zero);
        var repo = new Mock<ICartRepository>();

        repo.Setup(r => r.GetCommissionInvoiceForPeriodAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((CommissionInvoice?)null);
        repo.Setup(r => r.GetCommissionableEscrowEntriesAsync(sellerId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<EscrowLedgerEntry>
            {
                new() { Id = 1, SellerId = sellerId, OrderId = 10, SellerOrderId = 20, CommissionAmount = 10m, CreatedAt = now.AddDays(-20) },
                new() { Id = 2, SellerId = sellerId, OrderId = 11, SellerOrderId = 21, CommissionAmount = 5m, CreatedAt = now.AddDays(-10) }
            });
        repo.Setup(r => r.GetCommissionCorrectionsAsync(sellerId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<EscrowLedgerEntry>());
        repo.Setup(r => r.AddCommissionInvoiceAsync(It.IsAny<CommissionInvoice>()))
            .Callback<CommissionInvoice>(i => i.Id = 12)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var options = Options.Create(new CommissionInvoiceOptions
        {
            NumberPrefix = "INV",
            DefaultTaxRate = 0.2m,
            Currency = "PLN"
        });
        var service = new CommissionInvoiceService(repo.Object, options, new FixedTimeProvider(now));

        var invoice = await service.EnsurePreviousMonthInvoiceAsync(sellerId, sellerName);

        Assert.NotNull(invoice);
        Assert.Equal("INV-2026-00012", invoice!.Number);
        Assert.Equal(15m, invoice.Subtotal);
        Assert.Equal(3m, invoice.TaxAmount);
        Assert.Equal(18m, invoice.TotalAmount);
        Assert.False(invoice.IsCreditNote);
        Assert.Equal(CommissionInvoiceStatus.Issued, invoice.Status);
        Assert.Equal(2, invoice.Lines.Count);
    }

    [Fact]
    public async Task EnsurePreviousMonthInvoiceAsync_BuildsCreditNoteForCorrections()
    {
        var sellerId = "seller-2";
        var now = new DateTimeOffset(2026, 4, 2, 8, 0, 0, TimeSpan.Zero);
        var repo = new Mock<ICartRepository>();

        repo.Setup(r => r.GetCommissionInvoiceForPeriodAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((CommissionInvoice?)null);
        repo.Setup(r => r.GetCommissionableEscrowEntriesAsync(sellerId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<EscrowLedgerEntry>());
        repo.Setup(r => r.GetCommissionCorrectionsAsync(sellerId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<EscrowLedgerEntry>
            {
                new() { Id = 5, SellerId = sellerId, OrderId = 30, SellerOrderId = 40, CommissionAmount = 8m, ReleasedAt = now.AddDays(-5), Status = EscrowLedgerStatus.ReleasedToBuyer }
            });
        repo.Setup(r => r.AddCommissionInvoiceAsync(It.IsAny<CommissionInvoice>()))
            .Callback<CommissionInvoice>(i => i.Id = 7)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var service = new CommissionInvoiceService(
            repo.Object,
            Options.Create(new CommissionInvoiceOptions { DefaultTaxRate = 0.23m }),
            new FixedTimeProvider(now));

        var invoice = await service.EnsurePreviousMonthInvoiceAsync(sellerId, "Seller Two");

        Assert.NotNull(invoice);
        Assert.True(invoice!.IsCreditNote);
        Assert.Single(invoice.Lines);
        Assert.Equal(-8m, invoice.Subtotal);
        Assert.Equal(-1.84m, invoice.TaxAmount);
        Assert.Equal(-9.84m, invoice.TotalAmount);
        Assert.All(invoice.Lines, l => Assert.True(l.IsCorrection));
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
