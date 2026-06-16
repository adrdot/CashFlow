using CashFlow.Transactions.Application.Contracts;

namespace CashFlow.Transactions.Application.Abstractions;

public interface ITransactionService
{
    Task<CreateTransactionResult> CreateAsync(
        CreateTransactionRequest request,
        string userId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default
    );
}
