using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.Messaging.Abstractions;

namespace CashFlow.Transactions.Infrastructure.Messaging;

public sealed class NullTransactionEventPublisher : ITransactionEventPublisher
{
    public Task PublishAsync(
        TransactionRecordedEvent transactionEvent,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
