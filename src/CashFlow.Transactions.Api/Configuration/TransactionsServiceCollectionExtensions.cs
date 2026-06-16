using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Observability;
using CashFlow.Transactions.Infrastructure.Persistence;
using OpenTelemetry.Metrics;

namespace CashFlow.Transactions.Api.Configuration;

public static class TransactionsServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<EventStoreOptions>(configuration.GetSection(EventStoreOptions.SectionName));
        services.AddSingleton<RelaySubscriptionStats>();
        services.AddSingleton<TransactionMetrics>();
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddMeter(TransactionMetrics.MeterName));

        var eventStoreOptions = configuration.GetSection(EventStoreOptions.SectionName).Get<EventStoreOptions>()
            ?? new EventStoreOptions();
        var eventStoreConfigured = !string.IsNullOrWhiteSpace(eventStoreOptions.HttpEndpoint);

        if (eventStoreConfigured)
        {
            services.AddSingleton<IEventStoreTransactionWriter>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("eventstore");
                return new EventStoreTransactionWriter(
                    httpClient,
                    sp.GetRequiredService<TransactionMetrics>(),
                    sp.GetRequiredService<ILogger<EventStoreTransactionWriter>>());
            });
            services.AddSingleton<IEventStoreTransactionReader>(sp =>
            {
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("eventstore");
                return new EventStoreStreamReader(
                    httpClient,
                    sp.GetRequiredService<ILogger<EventStoreStreamReader>>());
            });
            services.AddScoped<CashFlow.Transactions.Application.Abstractions.ITransactionRepository, EventStoreTransactionRepository>();
        }
        else if (environment.IsDevelopment())
        {
            services.AddSingleton<CashFlow.Transactions.Application.Abstractions.ITransactionRepository, InMemoryTransactionRepository>();
        }
        else
        {
            throw new InvalidOperationException(
                "EventStore:HttpEndpoint is required outside development.");
        }

        services.AddSingleton<CashFlow.Transactions.Application.Abstractions.ITransactionEventPublisher, NullTransactionEventPublisher>();

        return services;
    }
}
