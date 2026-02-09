using System;

namespace SD.ProjectName.Modules.Cart.Domain;

public class PayoutSchedule
{
    public int Id { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public DateTimeOffset? ProcessingStartedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string Status { get; set; } = PayoutStatus.Scheduled;
    public string? ErrorReference { get; set; }
    public decimal TotalAmount { get; set; }
    public int AttemptCount { get; set; }

    public List<PayoutScheduleItem> Items { get; set; } = new();
}

public class PayoutScheduleItem
{
    public int Id { get; set; }
    public int PayoutScheduleId { get; set; }
    public PayoutSchedule PayoutSchedule { get; set; } = null!;
    public int EscrowLedgerEntryId { get; set; }
    public EscrowLedgerEntry? EscrowEntry { get; set; }
    public decimal Amount { get; set; }
}

public static class PayoutStatus
{
    public const string Scheduled = "scheduled";
    public const string Processing = "processing";
    public const string Paid = "paid";
    public const string Failed = "failed";
}
