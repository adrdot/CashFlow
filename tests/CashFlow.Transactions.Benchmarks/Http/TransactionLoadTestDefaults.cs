namespace CashFlow.Transactions.Benchmarks.Http;

/// <summary>
/// Default CLI values for transaction HTTP benchmarks. These are exploratory probes,
/// not the consolidation SLO (50 RPS / 5% loss — see ReportingSlo and reporting-slo.md).
/// </summary>
internal static class TransactionLoadTestDefaults
{
    public const int DefaultLoadRate = 10;

    public const int DefaultLoadDurationSeconds = 30;

    public const int DefaultStressStartRate = 10;

    public const int DefaultStressStepRate = 10;

    public const int DefaultStressMaxRate = 100;

    public const int DefaultStressStepSeconds = 15;

    /// <summary>Breakpoint detection threshold for stress runs (not a production SLO).</summary>
    public const double DefaultStressFailureThresholdPercent = 5.0;
}
