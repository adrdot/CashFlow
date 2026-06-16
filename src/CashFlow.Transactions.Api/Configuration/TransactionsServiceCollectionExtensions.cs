using Aspire.CashFlow.ServiceDefaults.Observability;
using CashFlow.Transactions.Infrastructure.Configuration;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.EventStore.Abstractions;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Messaging.Abstractions;
using CashFlow.Transactions.Infrastructure.Observability;
using CashFlow.Transactions.Infrastructure.Persistence;
using CashFlow.Transactions.Infrastructure.Persistence.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Api.Configuration;

public static class TransactionsServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.Configure<TransactionsOptions>(
            configuration.GetSection(TransactionsOptions.SectionName)
        );
        services.Configure<EventStoreOptions>(
            configuration.GetSection(EventStoreOptions.SectionName)
        );
        services.AddSingleton<RelaySubscriptionStats>();
        services.AddCashFlowMeter<TransactionMetrics>(TransactionMetrics.MeterName);

        var eventStoreOptions =
            configuration.GetSection(EventStoreOptions.SectionName).Get<EventStoreOptions>()
            ?? new EventStoreOptions();
        var eventStoreConfigured = !string.IsNullOrWhiteSpace(eventStoreOptions.HttpEndpoint);

        if (eventStoreConfigured)
        {
            services.AddSingleton<IEventStoreTransactionWriter>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                    .CreateClient("eventstore");
                return new EventStoreTransactionWriter(
                    httpClient,
                    sp.GetRequiredService<TransactionMetrics>(),
                    sp.GetRequiredService<ILogger<EventStoreTransactionWriter>>()
                );
            });
            services.AddSingleton<IEventStoreTransactionReader>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                    .CreateClient("eventstore");
                return new EventStoreStreamReader(
                    httpClient,
                    sp.GetRequiredService<IOptions<EventStoreOptions>>(),
                    sp.GetRequiredService<ILogger<EventStoreStreamReader>>()
                );
            });
            services.AddScoped<ITransactionRepository, EventStoreTransactionRepository>();
        }
        else if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();
        }
        else
        {
            throw new InvalidOperationException(
                "EventStore:HttpEndpoint is required outside development and testing."
            );
        }

        services.AddSingleton<ITransactionEventPublisher, NullTransactionEventPublisher>();

        return services;
    }
}
