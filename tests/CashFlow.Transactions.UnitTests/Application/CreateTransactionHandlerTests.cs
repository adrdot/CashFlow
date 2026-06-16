using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Application.UseCases;
using CashFlow.Transactions.Domain.Entities;

namespace CashFlow.Transactions.UnitTests.Application;

public sealed class CreateTransactionHandlerTests
{
    private const string UserId = "user-123";

    [Fact]
    public async Task CreateAsync_ReturnsSuccess_WhenRequestIsValid()
    {
        var repository = new FakeTransactionRepository();
        var handler = new CreateTransactionHandler(repository);

        var result = await handler.CreateAsync(new CreateTransactionRequest
        {
            Type = "Credit",
            Amount = 125.45m,
            Description = "Salary deposit",
            TransactionDate = new DateOnly(2026, 6, 12)
        }, UserId);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Transaction);
        Assert.Equal("Credit", result.Transaction!.Type);
        Assert.Equal(125.45m, result.Transaction.Amount);
        Assert.Single(repository.StoredTransactions);
    }

    [Fact]
    public async Task CreateAsync_ReturnsFailure_WhenTypeIsInvalid()
    {
        var repository = new FakeTransactionRepository();
        var handler = new CreateTransactionHandler(repository);

        var result = await handler.CreateAsync(new CreateTransactionRequest
        {
            Type = "Transfer",
            Amount = 20m,
            Description = "Invalid type",
            TransactionDate = new DateOnly(2026, 6, 12)
        }, UserId);

        Assert.False(result.Succeeded);
        Assert.Equal("Transaction type must be Debit or Credit.", result.ErrorMessage);
        Assert.Empty(repository.StoredTransactions);
    }

    [Fact]
    public async Task CreateAsync_ReturnsFailure_WhenUserIdIsMissing()
    {
        var repository = new FakeTransactionRepository();
        var handler = new CreateTransactionHandler(repository);

        var result = await handler.CreateAsync(new CreateTransactionRequest
        {
            Type = "Debit",
            Amount = 20m,
            Description = "Missing user",
            TransactionDate = new DateOnly(2026, 6, 12)
        }, string.Empty);

        Assert.False(result.Succeeded);
        Assert.Equal("Authenticated user identifier is required.", result.ErrorMessage);
    }

    private sealed class FakeTransactionRepository : ITransactionRepository
    {
        public List<CashFlowTransaction> StoredTransactions { get; } = [];

        public Task<PersistenceOutcome> SaveAsync(
            CashFlowTransaction transaction,
            string userId,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Task.FromResult(PersistenceOutcome.Failure("Authenticated user identifier is required."));
            }

            StoredTransactions.Add(transaction);
            return Task.FromResult(PersistenceOutcome.Success(transaction.Id));
        }
    }
}
