namespace CashFlow.Transactions.Infrastructure.Observability;

/// <summary>
/// Latest persistent-subscription stats polled from EventStore for relay observability.
/// </summary>
public sealed class RelaySubscriptionStats
{
    private long lagEvents;
    private long inFlightMessages;
    private long parkedMessages;

    public long LagEvents => Volatile.Read(ref lagEvents);

    public long InFlightMessages => Volatile.Read(ref inFlightMessages);

    public long ParkedMessages => Volatile.Read(ref parkedMessages);

    public void Update(long lagEvents, long inFlightMessages, long parkedMessages)
    {
        Volatile.Write(ref this.lagEvents, Math.Max(0, lagEvents));
        Volatile.Write(ref this.inFlightMessages, Math.Max(0, inFlightMessages));
        Volatile.Write(ref this.parkedMessages, Math.Max(0, parkedMessages));
    }
}
