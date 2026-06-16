namespace CashFlow.Reporting.Domain.Entities;

public sealed record DailySummary
{
    public string UserId { get; init; } = string.Empty;

    public DateOnly ReportDate { get; init; }

    public decimal TotalDebits { get; init; }

    public decimal TotalCredits { get; init; }

    public int DebitEntryCount { get; init; }

    public int CreditEntryCount { get; init; }

    public int TransactionVolume { get; init; }

    public DateTimeOffset LastUpdatedUtc { get; init; }

    public decimal ConsolidatedBalance => TotalCredits - TotalDebits;

    public bool HasData => TransactionVolume > 0;
}
