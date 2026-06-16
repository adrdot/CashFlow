using Microsoft.Extensions.Logging;

namespace CashFlow.Transactions.Infrastructure.Observability;

public static class TransactionLogEvents
{
    public static readonly EventId TransactionCreated = new(2001, nameof(TransactionCreated));

    public static readonly EventId TransactionPersistenceFailed = new(
        2002,
        nameof(TransactionPersistenceFailed)
    );

    public static readonly EventId TransactionIdempotentReplay = new(
        2003,
        nameof(TransactionIdempotentReplay)
    );

    public static readonly EventId OutboxEventPublished = new(2004, nameof(OutboxEventPublished));

    public static readonly EventId OutboxPublishFailed = new(2005, nameof(OutboxPublishFailed));

    public static readonly EventId OutboxQueueRefreshed = new(2006, nameof(OutboxQueueRefreshed));
}
