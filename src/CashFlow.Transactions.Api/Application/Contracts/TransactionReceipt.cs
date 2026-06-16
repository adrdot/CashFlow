namespace CashFlow.Transactions.Application.Contracts;

public sealed record TransactionReceipt
{
    public Guid Id { get; init; }

    public string Type { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateOnly TransactionDate { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}