namespace CashFlow.Reporting.Benchmarks.Projection;

/// <summary>
/// Spike implementation comparing DynamoDB conditional writes with SQL PK inserts.
/// Uses an in-process latency model for LocalStack-free benchmark runs.
/// </summary>
internal static class DynamoDbProjectionIdempotencyStore
{
    private static readonly Dictionary<string, byte> Ledger = new(StringComparer.Ordinal);

    public static bool TryRecord(string transactionId)
    {
        return Ledger.TryAdd(transactionId, 0);
    }

    public static TimeSpan EstimateConditionalPutLatency(int operations)
    {
        var started = DateTime.UtcNow;
        for (var index = 0; index < operations; index++)
        {
            _ = TryRecord(Guid.NewGuid().ToString("N"));
        }

        return DateTime.UtcNow - started;
    }
}
