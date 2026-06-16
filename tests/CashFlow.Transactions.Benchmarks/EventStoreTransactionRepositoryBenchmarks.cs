using BenchmarkDotNet.Attributes;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.ValueObjects;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.Observability;
using CashFlow.Transactions.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.Metrics;

namespace CashFlow.Transactions.Benchmarks;

[MemoryDiagnoser]
public class EventStoreTransactionRepositoryBenchmarks : IDisposable
{
    private FakeBenchmarkEventStore eventStore = null!;
    private EventStoreTransactionRepository repository = null!;
    private CashFlowTransaction transaction = null!;

    [GlobalSetup]
    public void Setup()
    {
        eventStore = new FakeBenchmarkEventStore();

        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<RelaySubscriptionStats>();
        using var provider = services.BuildServiceProvider();
        var metrics = new TransactionMetrics(
            provider.GetRequiredService<IMeterFactory>(),
            provider.GetRequiredService<RelaySubscriptionStats>());

        repository = new EventStoreTransactionRepository(
            eventStore,
            eventStore,
            metrics,
            NullLogger<EventStoreTransactionRepository>.Instance);
        transaction = new CashFlowTransaction
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Debit,
            Amount = 75.25m,
            Description = "Benchmark repository save",
            OccurredOn = new DateOnly(2026, 6, 14),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    [Benchmark(Description = "SaveAsync with EventStore idempotency store")]
    public async Task<PersistenceOutcome> SaveAsync()
    {
        transaction = transaction with { Id = Guid.NewGuid() };
        return await repository.SaveAsync(transaction, "benchmark-user@cashflow.local");
    }

    public void Dispose()
    {
    }

    private sealed class FakeBenchmarkEventStore : IEventStoreTransactionWriter, IEventStoreTransactionReader
    {
        public Task AppendAsync(
            Application.Contracts.TransactionRecordedEvent transactionEvent,
            Guid eventId,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Application.Contracts.TransactionRecordedEvent?> TryGetByEventIdAsync(
            string userId,
            Guid eventId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Application.Contracts.TransactionRecordedEvent?>(null);
    }
}
