using CashFlow.Transactions.Application.Contracts;

namespace CashFlow.Transactions.Application.Abstractions;

public interface ITransactionEventPublisher
{
    Task PublishAsync(TransactionRecordedEvent transactionEvent, CancellationToken cancellationToken = default);
}