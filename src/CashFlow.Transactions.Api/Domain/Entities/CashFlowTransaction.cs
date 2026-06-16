using CashFlow.Transactions.Domain.ValueObjects;

namespace CashFlow.Transactions.Domain.Entities;

public sealed record CashFlowTransaction
{
    public Guid Id { get; init; }

    public TransactionType Type { get; init; }

    public decimal Amount { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateOnly OccurredOn { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
