namespace CashFlow.Transactions.Domain.ValueObjects;

public enum TransactionType
{
    Debit = 1,
    Credit = 2,
}

public static class TransactionTypeParser
{
    public static bool TryParse(string? value, out TransactionType transactionType)
    {
        if (Enum.TryParse<TransactionType>(value, ignoreCase: true, out transactionType))
        {
            return transactionType is TransactionType.Debit or TransactionType.Credit;
        }

        transactionType = default;
        return false;
    }
}
