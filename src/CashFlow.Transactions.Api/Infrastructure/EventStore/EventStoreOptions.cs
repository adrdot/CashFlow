namespace CashFlow.Transactions.Infrastructure.EventStore;

public sealed class EventStoreOptions
{
    public const string SectionName = "EventStore";

    public string ConnectionString { get; set; } = "esdb://127.0.0.1:2113?tls=false";

    public string HttpEndpoint { get; set; } = "http://127.0.0.1:2113";

    public string SubscriptionGroupName { get; set; } = "cashflow-sns-relay";

    public string StreamNamePrefix { get; set; } = "cashflow-";
}
