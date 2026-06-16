using CashFlow.Transactions.Application.Contracts;

namespace CashFlow.Transactions.Infrastructure.EventStore;

public interface IEventStoreTransactionReader
{
    Task<TransactionRecordedEvent?> TryGetByEventIdAsync(
        string userId,
        Guid eventId,
        CancellationToken cancellationToken = default);
}
