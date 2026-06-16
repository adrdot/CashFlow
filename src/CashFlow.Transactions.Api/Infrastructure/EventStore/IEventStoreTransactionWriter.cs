using CashFlow.Transactions.Application.Contracts;

namespace CashFlow.Transactions.Infrastructure.EventStore;

public interface IEventStoreTransactionWriter
{
    Task AppendAsync(
        TransactionRecordedEvent transactionEvent,
        Guid eventId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}
