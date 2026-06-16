using CashFlow.Transactions.Infrastructure.Persistence.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Infrastructure.EventStore;

namespace CashFlow.Transactions.Infrastructure.Persistence;

public sealed class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        Guid,
        CashFlowTransaction
    > transactions = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        string,
        TransactionRecordedEvent
    > idempotentEvents = new();

    public Task<PersistenceOutcome> SaveAsync(
        CashFlowTransaction transaction,
        string userId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(
                PersistenceOutcome.Failure("Authenticated user identifier is required.")
            );
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var idempotencyLookupKey = BuildIdempotencyLookupKey(userId, idempotencyKey);
            if (idempotentEvents.TryGetValue(idempotencyLookupKey, out var existing))
            {
                return Task.FromResult(PersistenceOutcome.Replay(ToSnapshot(existing)));
            }

            var recordedEvent = ToRecordedEvent(transaction, userId);
            if (!idempotentEvents.TryAdd(idempotencyLookupKey, recordedEvent))
            {
                if (idempotentEvents.TryGetValue(idempotencyLookupKey, out existing))
                {
                    return Task.FromResult(PersistenceOutcome.Replay(ToSnapshot(existing)));
                }
            }

            transactions.TryAdd(transaction.Id, transaction);
            return Task.FromResult(PersistenceOutcome.Success(transaction.Id));
        }

        var stored = transactions.TryAdd(transaction.Id, transaction);
        return Task.FromResult(
            stored
                ? PersistenceOutcome.Success(transaction.Id)
                : PersistenceOutcome.Failure(
                    "A transaction with the same identifier already exists."
                )
        );
    }

    private static string BuildIdempotencyLookupKey(string userId, string idempotencyKey) =>
        $"{userId.Trim()}:{idempotencyKey.Trim()}";

    private static TransactionRecordedEvent ToRecordedEvent(
        CashFlowTransaction transaction,
        string userId
    ) =>
        new()
        {
            TransactionId = transaction.Id,
            UserId = userId,
            Type = transaction.Type.ToString(),
            Amount = transaction.Amount,
            Description = transaction.Description,
            TransactionDate = transaction.OccurredOn,
            CreatedAtUtc = transaction.CreatedAtUtc,
        };

    private static PersistedTransactionSnapshot ToSnapshot(
        TransactionRecordedEvent recordedEvent
    ) =>
        new(
            recordedEvent.TransactionId,
            recordedEvent.Type,
            recordedEvent.Amount,
            recordedEvent.Description,
            recordedEvent.TransactionDate,
            recordedEvent.CreatedAtUtc
        );
}
