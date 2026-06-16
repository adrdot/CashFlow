namespace CashFlow.Transactions.Api.Observability;

/// <summary>
/// Service level objectives for the Transactions API (spec 002-feature-cash-flow).
/// Throughput (50 RPS) and consolidation loss tolerance (5%) belong to ReportingSlo / reporting-slo.md.
/// </summary>
public static class TransactionSlo
{
    /// <summary>Target mean/p95 persistence latency for successful writes (EventStore append).</summary>
    public const int MaxPersistenceLatencyMs = 200;

    public const double PersistenceLatencyPercentile = 95.0;

    /// <summary>Target server error rate for the write API (5xx only; excludes client validation 4xx).</summary>
    public const double MaxServerErrorPercent = 1.0;

    public const int EndToEndRecordingSeconds = 5;

    public const double EndToEndRecordingPercentile = 95.0;

    /// <summary>Maximum sustained SNS publish failures before paging (minutes).</summary>
    public const int PublishFailureSustainedMinutes = 5;

    /// <summary>Legacy alias used by exploratory HTTP load gates (mean latency check).</summary>
    public const int MaxMeanLatencyMs = MaxPersistenceLatencyMs;
}
