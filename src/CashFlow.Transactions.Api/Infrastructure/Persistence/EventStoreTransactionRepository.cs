using System.Diagnostics;
using CashFlow.Transactions.Infrastructure.EventStore.Abstractions;
using CashFlow.Transactions.Infrastructure.Persistence.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace CashFlow.Transactions.Infrastructure.Persistence;

public sealed class EventStoreTransactionRepository(
    IEventStoreTransactionWriter eventStoreWriter,
    IEventStoreTransactionReader eventStoreReader,
    TransactionMetrics metrics,
    ILogger<EventStoreTransactionRepository> logger
) : ITransactionRepository
{
    public async Task<PersistenceOutcome> SaveAsync(
        CashFlowTransaction transaction,
        string userId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var persisted = false;

        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return PersistenceOutcome.Failure("Authenticated user identifier is required.");
            }

            var hasIdempotencyKey = !string.IsNullOrWhiteSpace(idempotencyKey);
            var eventId = hasIdempotencyKey
                ? IdempotencyEventId.Create(userId, idempotencyKey!)
                : transaction.Id;

            if (hasIdempotencyKey)
            {
                var existing = await eventStoreReader.TryGetByEventIdAsync(
                    userId,
                    eventId,
                    cancellationToken
                );
                if (existing is not null)
                {
                    return CompleteIdempotentReplay(existing, stopwatch);
                }
            }
            else
            {
                var existing = await eventStoreReader.TryGetByEventIdAsync(
                    userId,
                    eventId,
                    cancellationToken
                );
                if (existing is not null)
                {
                    return PersistenceOutcome.Failure(
                        "A transaction with the same identifier already exists."
                    );
                }
            }

            var recordedEvent = new TransactionRecordedEvent
            {
                TransactionId = transaction.Id,
                UserId = userId,
                Type = transaction.Type.ToString(),
                Amount = transaction.Amount,
                Description = transaction.Description,
                TransactionDate = transaction.OccurredOn,
                CreatedAtUtc = transaction.CreatedAtUtc,
            };

            try
            {
                await eventStoreWriter.AppendAsync(
                    recordedEvent,
                    eventId,
                    idempotencyKey,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                metrics.IncrementPersistenceFailure("eventstore");
                logger.LogWarning(
                    TransactionLogEvents.TransactionPersistenceFailed,
                    ex,
                    "EventStore append failed for transaction {TransactionId}.",
                    transaction.Id
                );

                return PersistenceOutcome.Failure(
                    $"Transaction could not be recorded in EventStore: {ex.Message}"
                );
            }

            if (hasIdempotencyKey)
            {
                var stored = await eventStoreReader.TryGetByEventIdAsync(
                    userId,
                    eventId,
                    cancellationToken
                );
                if (stored is not null && stored.TransactionId != transaction.Id)
                {
                    return CompleteIdempotentReplay(stored, stopwatch);
                }
            }

            metrics.IncrementCreatedTransactions(transaction.Type.ToString());
            logger.LogInformation(
                TransactionLogEvents.TransactionCreated,
                "Transaction {TransactionId} persisted for user {UserId}.",
                transaction.Id,
                userId
            );

            persisted = true;
            return PersistenceOutcome.Success(transaction.Id);
        }
        finally
        {
            metrics.RecordPersistenceDuration(stopwatch.Elapsed, persisted ? "success" : "failure");
        }
    }

    private PersistenceOutcome CompleteIdempotentReplay(
        TransactionRecordedEvent existing,
        Stopwatch stopwatch
    )
    {
        metrics.IncrementIdempotentReplays();
        logger.LogInformation(
            TransactionLogEvents.TransactionIdempotentReplay,
            "Served idempotent replay for transaction {TransactionId}.",
            existing.TransactionId
        );

        metrics.RecordPersistenceDuration(stopwatch.Elapsed, "replay");
        return PersistenceOutcome.Replay(ToSnapshot(existing));
    }

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
