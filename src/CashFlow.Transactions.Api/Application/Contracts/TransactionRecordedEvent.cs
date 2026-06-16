namespace CashFlow.Transactions.Application.Contracts;

public sealed record TransactionRecordedEvent
{
    public Guid TransactionId { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateOnly TransactionDate { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}