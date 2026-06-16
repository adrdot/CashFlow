using global::EventStore.Client;

namespace CashFlow.Transactions.Infrastructure.EventStore;

internal static class EventStoreClientFactory
{
    public static EventStorePersistentSubscriptionsClient Create(EventStoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("EventStore:ConnectionString is required.");
        }

        var settings = EventStoreClientSettings.Create(options.ConnectionString);
        return new EventStorePersistentSubscriptionsClient(settings);
    }
}
