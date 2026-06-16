namespace CashFlow.Reporting.Infrastructure.Observability;

/// <summary>
/// Latest SQS queue depth polled for pipeline observability.
/// </summary>
public sealed class ReportingQueueStats
{
    private long visibleMessages;
    private long inFlightMessages;

    public long VisibleMessages => Volatile.Read(ref visibleMessages);

    public long InFlightMessages => Volatile.Read(ref inFlightMessages);

    public void Update(long visibleMessages, long inFlightMessages)
    {
        Volatile.Write(ref this.visibleMessages, Math.Max(0, visibleMessages));
        Volatile.Write(ref this.inFlightMessages, Math.Max(0, inFlightMessages));
    }
}
