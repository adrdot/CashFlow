namespace CashFlow.Transactions.Infrastructure.EventStore;

public sealed class EventStoreOptions
{
    public const string SectionName = "EventStore";

    public string ConnectionString { get; set; } = string.Empty;

    public string HttpEndpoint { get; set; } = string.Empty;

    public string SubscriptionGroupName { get; set; } = string.Empty;

    public string StreamNamePrefix { get; set; } = string.Empty;

    public int HttpTimeoutSeconds { get; set; }

    public int SubscriptionMonitorPollSeconds { get; set; }

    public int SubscriptionBufferSize { get; set; }

    public int BackwardPageSize { get; set; }

    public int MaxReadPages { get; set; }

    public string PersistentSubscriptionStream { get; set; } = string.Empty;
}
