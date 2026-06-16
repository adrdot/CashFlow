using CashFlow.Transactions.Application.Contracts;

namespace CashFlow.Transactions.Infrastructure.Messaging.Abstractions;

public interface ITransactionEventPublisher
{
    Task PublishAsync(
        TransactionRecordedEvent transactionEvent,
        CancellationToken cancellationToken = default
    );
}
