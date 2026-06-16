namespace CashFlow.Transactions.Domain.Entities;

public sealed record PersistenceOutcome
{
    public static PersistenceOutcome Success(Guid transactionId) =>
        new() { IsPersisted = true, TransactionId = transactionId };

    public static PersistenceOutcome Replay(PersistedTransactionSnapshot snapshot) =>
        new()
        {
            IsPersisted = true,
            IsReplay = true,
            TransactionId = snapshot.TransactionId,
            ReplayedSnapshot = snapshot,
        };

    public static PersistenceOutcome Failure(string reason) =>
        new() { IsPersisted = false, FailureReason = reason };

    public bool IsPersisted { get; init; }

    public bool IsReplay { get; init; }

    public Guid? TransactionId { get; init; }

    public PersistedTransactionSnapshot? ReplayedSnapshot { get; init; }

    public string? FailureReason { get; init; }
}
