using Amazon.SimpleNotificationService;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Observability;
using OpenTelemetry.Metrics;

namespace CashFlow.Transactions.Api.Configuration;

public static class TransactionsRelayServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionsRelayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));
        services.Configure<EventStoreOptions>(configuration.GetSection(EventStoreOptions.SectionName));
        services.AddSingleton<RelaySubscriptionStats>();
        services.AddSingleton<TransactionMetrics>();
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddMeter(TransactionMetrics.MeterName));

        var eventStoreOptions = configuration.GetSection(EventStoreOptions.SectionName).Get<EventStoreOptions>()
            ?? new EventStoreOptions();
        if (string.IsNullOrWhiteSpace(eventStoreOptions.ConnectionString)
            && string.IsNullOrWhiteSpace(eventStoreOptions.HttpEndpoint))
        {
            throw new InvalidOperationException(
                "EventStore:ConnectionString or EventStore:HttpEndpoint is required for the relay worker.");
        }

        var messagingOptions = configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>()
            ?? new MessagingOptions();
        if (!messagingOptions.Enabled || string.IsNullOrWhiteSpace(messagingOptions.SnsTopicArn))
        {
            throw new InvalidOperationException(
                "Messaging:Enabled and Messaging:SnsTopicArn are required for the relay worker.");
        }

        services.AddSingleton<EventStorePersistentSubscriptionStatsReader>();
        services.AddSingleton(_ => EventStoreClientFactory.Create(eventStoreOptions));
        services.AddSingleton<IAmazonSimpleNotificationService>(_ => SnsClientFactory.Create(messagingOptions));
        services.AddSingleton<CashFlow.Transactions.Application.Abstractions.ITransactionEventPublisher, SnsTransactionEventPublisher>();
        services.AddHostedService<EventStoreToSnsRelayBackgroundService>();
        services.AddHostedService<EventStoreRelaySubscriptionMonitorBackgroundService>();

        return services;
    }
}
