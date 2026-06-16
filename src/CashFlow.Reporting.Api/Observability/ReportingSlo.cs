namespace CashFlow.Reporting.Api.Observability;

/// <summary>
/// Service level objectives for consolidated reporting (constitution + spec 003-feature-consolidated-report).
/// </summary>
public static class ReportingSlo
{
    public const int TargetRequestsPerSecond = 50;

    public const double MaxFailurePercent = 5.0;

    public const double MaxServerErrorPercent = 1.0;

    public const int MaxMeanLatencyMs = 200;

    /// <summary>Cached read p50 gate (steady state after warm-up).</summary>
    public const int MaxCachedP50LatencyMs = 200;

    /// <summary>Cached read p95 gate (spec 003 SC-002).</summary>
    public const int MaxCachedP95LatencyMs = 2000;

    public const int CachedReportSeconds = 2;

    public const int UncachedReportSeconds = 5;

    public const double ReportLatencyPercentile = 95.0;
}
