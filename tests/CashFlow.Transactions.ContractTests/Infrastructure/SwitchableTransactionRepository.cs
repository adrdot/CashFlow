using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Infrastructure.Persistence;
using CashFlow.Transactions.Infrastructure.Persistence.Abstractions;

namespace CashFlow.Transactions.ContractTests.Infrastructure;

internal sealed class SwitchableTransactionRepository : ITransactionRepository
{
    private readonly InMemoryTransactionRepository inMemoryRepository = new();
    private readonly FailingTransactionRepository failingRepository = new();

    public bool PersistenceFails { get; set; }

    public Task<PersistenceOutcome> SaveAsync(
        CashFlowTransaction transaction,
        string userId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default
    )
    {
        return PersistenceFails
            ? failingRepository.SaveAsync(transaction, userId, idempotencyKey, cancellationToken)
            : inMemoryRepository.SaveAsync(transaction, userId, idempotencyKey, cancellationToken);
    }
}
