namespace CashFlow.Transactions.Infrastructure.Configuration;

public sealed class TransactionsOptions
{
    public const string SectionName = "Transactions";

    public int PersistenceRetryAfterSeconds { get; init; }
}
