namespace CashFlow.Transactions.Domain.Entities;

public sealed record PersistedTransactionSnapshot(
    Guid TransactionId,
    string Type,
    decimal Amount,
    string Description,
    DateOnly TransactionDate,
    DateTimeOffset CreatedAtUtc);
