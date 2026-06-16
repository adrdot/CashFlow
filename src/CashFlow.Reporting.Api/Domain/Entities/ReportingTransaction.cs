namespace CashFlow.Reporting.Domain.Entities;

public enum ReportTransactionType
{
    Debit = 1,
    Credit = 2,
}

public sealed record ReportingTransaction
{
    public Guid Id { get; init; }

    public ReportTransactionType Type { get; init; }

    public decimal Amount { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateOnly OccurredOn { get; init; }
}
