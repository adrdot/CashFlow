using CashFlow.Transactions.Infrastructure.Messaging.Abstractions;
using CashFlow.Transactions.Application.Contracts;

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
