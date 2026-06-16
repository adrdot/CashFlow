using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.Persistence.Abstractions;
using CashFlow.Transactions.Domain.Entities;
using CashFlow.Transactions.Domain.ValueObjects;

namespace CashFlow.Transactions.Application.UseCases;

public sealed class CreateTransactionHandler(ITransactionRepository repository)
    : ITransactionService
{
    public async Task<CreateTransactionResult> CreateAsync(
        CreateTransactionRequest request,
        string userId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return CreateTransactionResult.Failure("Authenticated user identifier is required.");
        }

        if (!TransactionTypeParser.TryParse(request.Type, out var transactionType))
        {
            return CreateTransactionResult.Failure("Transaction type must be Debit or Credit.");
        }

        if (request.Amount <= 0)
        {
            return CreateTransactionResult.Failure("Amount must be greater than zero.");
        }

        var description = request.Description.Trim();
        if (description.Length < 3)
        {
            return CreateTransactionResult.Failure(
                "Description must contain at least 3 characters."
            );
        }

        if (request.TransactionDate is null)
        {
            return CreateTransactionResult.Failure("Transaction date is required.");
        }

        var transaction = new CashFlowTransaction
        {
            Id = Guid.NewGuid(),
            Type = transactionType,
            Amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Description = description,
            OccurredOn = request.TransactionDate.Value,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        var persistenceOutcome = await repository.SaveAsync(
            transaction,
            userId,
            idempotencyKey,
            cancellationToken
        );
        if (!persistenceOutcome.IsPersisted)
        {
            return CreateTransactionResult.Failure(
                persistenceOutcome.FailureReason ?? "Transaction could not be recorded."
            );
        }

        if (persistenceOutcome.IsReplay && persistenceOutcome.ReplayedSnapshot is not null)
        {
            return CreateTransactionResult.Success(ToReceipt(persistenceOutcome.ReplayedSnapshot));
        }

        return CreateTransactionResult.Success(
            new TransactionReceipt
            {
                Id = transaction.Id,
                Type = transaction.Type.ToString(),
                Amount = transaction.Amount,
                Description = transaction.Description,
                TransactionDate = transaction.OccurredOn,
                CreatedAtUtc = transaction.CreatedAtUtc,
            }
        );
    }

    private static TransactionReceipt ToReceipt(PersistedTransactionSnapshot snapshot) =>
        new()
        {
            Id = snapshot.TransactionId,
            Type = snapshot.Type,
            Amount = snapshot.Amount,
            Description = snapshot.Description,
            TransactionDate = snapshot.TransactionDate,
            CreatedAtUtc = snapshot.CreatedAtUtc,
        };
}
