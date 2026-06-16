using global::EventStore.Client;

namespace CashFlow.Transactions.Infrastructure.EventStore;

internal static class EventStoreClientFactory
{
    public static EventStorePersistentSubscriptionsClient Create(EventStoreOptions options)
    {
        var connectionString = string.IsNullOrWhiteSpace(options.ConnectionString)
            ? "esdb://127.0.0.1:2113?tls=false"
            : options.ConnectionString;

        var settings = EventStoreClientSettings.Create(connectionString);
        return new EventStorePersistentSubscriptionsClient(settings);
    }
}
