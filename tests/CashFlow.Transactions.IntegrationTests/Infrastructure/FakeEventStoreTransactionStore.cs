using System.Collections.Concurrent;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.EventStore;

namespace CashFlow.Transactions.IntegrationTests.Infrastructure;

public sealed class FakeEventStoreTransactionStore : IEventStoreTransactionWriter, IEventStoreTransactionReader
{
    private readonly ConcurrentDictionary<(string UserId, Guid EventId), TransactionRecordedEvent> events = new();

    public IReadOnlyList<(Guid EventId, TransactionRecordedEvent Event)> AppendedEvents =>
        events.Select(pair => (pair.Key.EventId, pair.Value)).ToList();

    public Task AppendAsync(
        TransactionRecordedEvent transactionEvent,
        Guid eventId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        events.GetOrAdd((transactionEvent.UserId, eventId), transactionEvent);
        return Task.CompletedTask;
    }

    public Task<TransactionRecordedEvent?> TryGetByEventIdAsync(
        string userId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        events.TryGetValue((userId, eventId), out var found);
        return Task.FromResult(found);
    }
}

internal sealed class FailingEventStoreWriter : IEventStoreTransactionWriter
{
    public Task AppendAsync(
        TransactionRecordedEvent transactionEvent,
        Guid eventId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("EventStore is unavailable.");
}

internal sealed class FailingEventStoreReader : IEventStoreTransactionReader
{
    public Task<TransactionRecordedEvent?> TryGetByEventIdAsync(
        string userId,
        Guid eventId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<TransactionRecordedEvent?>(null);
}
