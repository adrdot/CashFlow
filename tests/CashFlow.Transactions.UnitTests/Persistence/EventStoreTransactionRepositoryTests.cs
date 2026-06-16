using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.ValueObjects;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.EventStore.Abstractions;
using CashFlow.Transactions.Infrastructure.Observability;
using CashFlow.Transactions.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CashFlow.Transactions.UnitTests.Persistence;

public sealed class EventStoreTransactionRepositoryTests
{
    [Fact]
    public async Task SaveAsync_ReturnsReplay_WhenIdempotencyKeyAlreadyExists()
    {
        var store = new InMemoryEventStore();
        var eventId = IdempotencyEventId.Create("user@cashflow.local", "idem-001");
        var existing = new TransactionRecordedEvent
        {
            TransactionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            UserId = "user@cashflow.local",
            Type = "Debit",
            Amount = 10m,
            Description = "existing",
            TransactionDate = new DateOnly(2026, 6, 14),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        store.Seed("user@cashflow.local", eventId, existing);

        var repository = CreateRepository(store);
        var transaction = new CashFlowTransaction
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Credit,
            Amount = 999m,
            Description = "should replay",
            OccurredOn = new DateOnly(2026, 1, 1),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        var outcome = await repository.SaveAsync(transaction, "user@cashflow.local", "idem-001");

        Assert.True(outcome.IsReplay);
        Assert.Equal(existing.TransactionId, outcome.ReplayedSnapshot?.TransactionId);
        Assert.Empty(store.AppendedEvents);
    }

    private static EventStoreTransactionRepository CreateRepository(InMemoryEventStore store)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<RelaySubscriptionStats>();
        using var provider = services.BuildServiceProvider();
        var metrics = new TransactionMetrics(
            provider.GetRequiredService<IMeterFactory>(),
            provider.GetRequiredService<RelaySubscriptionStats>()
        );
        return new EventStoreTransactionRepository(
            store,
            store,
            metrics,
            NullLogger<EventStoreTransactionRepository>.Instance
        );
    }

    private sealed class InMemoryEventStore
        : IEventStoreTransactionWriter,
            IEventStoreTransactionReader
    {
        private readonly ConcurrentDictionary<
            (string UserId, Guid EventId),
            TransactionRecordedEvent
        > events = new();
        private readonly List<(Guid EventId, TransactionRecordedEvent Event)> appendedEvents = [];

        public IReadOnlyList<(Guid EventId, TransactionRecordedEvent Event)> AppendedEvents =>
            appendedEvents;

        public void Seed(string userId, Guid eventId, TransactionRecordedEvent recordedEvent) =>
            events[(userId, eventId)] = recordedEvent;

        public Task AppendAsync(
            TransactionRecordedEvent transactionEvent,
            Guid eventId,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default
        )
        {
            if (!events.ContainsKey((transactionEvent.UserId, eventId)))
            {
                appendedEvents.Add((eventId, transactionEvent));
            }

            events.GetOrAdd((transactionEvent.UserId, eventId), transactionEvent);
            return Task.CompletedTask;
        }

        public Task<TransactionRecordedEvent?> TryGetByEventIdAsync(
            string userId,
            Guid eventId,
            CancellationToken cancellationToken = default
        )
        {
            events.TryGetValue((userId, eventId), out var found);
            return Task.FromResult(found);
        }
    }
}
