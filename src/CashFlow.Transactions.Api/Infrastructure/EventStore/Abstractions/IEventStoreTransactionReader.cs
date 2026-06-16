using CashFlow.Transactions.Application.Contracts;

namespace CashFlow.Transactions.Infrastructure.EventStore.Abstractions;

public interface IEventStoreTransactionReader
{
    Task<TransactionRecordedEvent?> TryGetByEventIdAsync(
        string userId,
        Guid eventId,
        CancellationToken cancellationToken = default
    );
}
