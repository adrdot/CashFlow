using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CashFlow.Transactions.ContractTests.Infrastructure;

internal sealed class FakeEventStoreTransactionStore : IEventStoreTransactionWriter, IEventStoreTransactionReader
{
    public Task AppendAsync(
        TransactionRecordedEvent transactionEvent,
        Guid eventId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<TransactionRecordedEvent?> TryGetByEventIdAsync(
        string userId,
        Guid eventId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<TransactionRecordedEvent?>(null);
}

internal sealed class FailingTransactionRepository : ITransactionRepository
{
    public Task<PersistenceOutcome> SaveAsync(
        CashFlow.Transactions.Domain.Entities.CashFlowTransaction transaction,
        string userId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PersistenceOutcome.Failure("Transaction could not be recorded in EventStore: simulated failure."));
}

public sealed class TransactionsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public TransactionsWebApplicationFactory(bool useFailingRepository = false)
    {
        UseFailingRepository = useFailingRepository;
    }

    public bool UseFailingRepository { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:HttpEndpoint"] = string.Empty,
                ["Messaging:Enabled"] = "false",
                ["Cognito:Enabled"] = "false",
                ["Jwt:Issuer"] = TestJwtTokenHelper.DefaultIssuer,
                ["Jwt:Audience"] = TestJwtTokenHelper.DefaultAudience,
                ["Jwt:SigningKey"] = TestJwtTokenHelper.DefaultSigningKey,
                ["Security:RateLimitingEnabled"] = "false"
            });
        });

        if (UseFailingRepository)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransactionRepository>();
                services.AddSingleton<ITransactionRepository, FailingTransactionRepository>();
            });
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}
