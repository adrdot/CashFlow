using CashFlow.Transactions.Domain.Entities;

namespace CashFlow.Transactions.Application.Abstractions;

public interface ITransactionRepository
{
    Task<PersistenceOutcome> SaveAsync(
        CashFlowTransaction transaction,
        string userId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}