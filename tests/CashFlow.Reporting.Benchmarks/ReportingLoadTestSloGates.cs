namespace CashFlow.Reporting.Benchmarks;

/// <summary>
/// Load-test gates aligned with <c>docs/reporting-slo.md</c> (not duplicated in application code).
/// </summary>
internal static class ReportingLoadTestSloGates
{
    public const int TargetRequestsPerSecond = 50;

    public const double MaxFailurePercent = 5.0;

    public const int MaxCachedP50LatencyMs = 200;

    public const int MaxCachedP95LatencyMs = 2000;
}
