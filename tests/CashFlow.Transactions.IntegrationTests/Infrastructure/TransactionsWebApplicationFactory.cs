using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.EventStore.Abstractions;
using CashFlow.Transactions.Infrastructure.Persistence;
using CashFlow.Transactions.Infrastructure.Persistence.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CashFlow.Transactions.IntegrationTests.Infrastructure;

public enum TransactionsTestMode
{
    InMemoryRepository,
    EventStorePersistence,
    FailingEventStore,
}

public sealed class TransactionsWebApplicationFactory
    : WebApplicationFactory<Program>,
        IAsyncLifetime
{
    public TransactionsWebApplicationFactory(
        TransactionsTestMode mode = TransactionsTestMode.InMemoryRepository
    )
    {
        Mode = mode;
    }

    public TransactionsTestMode Mode { get; }

    public FakeEventStoreTransactionStore? EventStore { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TransactionsTestEnvironment.IsolateFromAspireEnvironment();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.Sources.Clear();
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Observability:PrometheusEnabled"] = "false",
                        ["EventStore:HttpEndpoint"] =
                            Mode == TransactionsTestMode.InMemoryRepository
                                ? string.Empty
                                : "http://127.0.0.1:2113",
                        ["Messaging:Enabled"] = "false",
                        ["Cognito:Enabled"] = "false",
                        ["Cognito:UserPoolId"] = "",
                        ["Cognito:ClientId"] = "",
                        ["Cognito:ServiceUrl"] = "",
                        ["Jwt:Issuer"] = TestJwtTokenHelper.DefaultIssuer,
                        ["Jwt:Audience"] = TestJwtTokenHelper.DefaultAudience,
                        ["Jwt:SigningKey"] = TestJwtTokenHelper.DefaultSigningKey,
                        ["Security:RateLimitingEnabled"] = "false",
                    }
                );
            }
        );

        if (
            Mode
            is TransactionsTestMode.EventStorePersistence
                or TransactionsTestMode.FailingEventStore
        )
        {
            builder.ConfigureTestServices(ReplaceEventStoreServices);
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    private void ReplaceEventStoreServices(IServiceCollection services)
    {
        services.RemoveAll<ITransactionRepository>();
        services.RemoveAll<IEventStoreTransactionWriter>();
        services.RemoveAll<IEventStoreTransactionReader>();

        if (Mode == TransactionsTestMode.FailingEventStore)
        {
            services.AddSingleton<IEventStoreTransactionWriter, FailingEventStoreWriter>();
            services.AddSingleton<IEventStoreTransactionReader, FailingEventStoreReader>();
            services.AddScoped<ITransactionRepository, EventStoreTransactionRepository>();
            return;
        }

        EventStore = new FakeEventStoreTransactionStore();
        services.AddSingleton<IEventStoreTransactionWriter>(EventStore);
        services.AddSingleton<IEventStoreTransactionReader>(EventStore);
        services.AddScoped<ITransactionRepository, EventStoreTransactionRepository>();
    }
}
