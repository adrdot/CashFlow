namespace CashFlow.Reporting.Infrastructure.Persistence.Entities;

public sealed class ProjectedTransactionEntity
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int Type { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateOnly OccurredOn { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class DailySummaryEntity
{
    public string UserId { get; set; } = string.Empty;

    public DateOnly ReportDate { get; set; }

    public decimal TotalDebits { get; set; }

    public decimal TotalCredits { get; set; }

    public int DebitEntryCount { get; set; }

    public int CreditEntryCount { get; set; }

    public int TransactionVolume { get; set; }

    public DateTimeOffset LastUpdatedUtc { get; set; }
}
