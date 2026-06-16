namespace CashFlow.Transactions.Benchmarks;

/// <summary>
/// Load-test gates aligned with <c>docs/transactions-slo.md</c> (not duplicated in application code).
/// </summary>
internal static class TransactionLoadTestSloGates
{
    public const int MaxMeanLatencyMs = 200;
}
