namespace CashFlow.Transactions.Application.Contracts;

public sealed record CreateTransactionResult
{
    public static CreateTransactionResult Success(TransactionReceipt transaction) =>
        new() { Succeeded = true, Transaction = transaction };

    public static CreateTransactionResult Failure(string errorMessage) =>
        new() { Succeeded = false, ErrorMessage = errorMessage };

    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public TransactionReceipt? Transaction { get; init; }
}
