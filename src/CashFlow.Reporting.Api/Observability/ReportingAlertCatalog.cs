namespace CashFlow.Reporting.Api.Observability;

/// <summary>
/// Operational alert identifiers and PromQL expressions for Reporting API and Worker.
/// Mirrors <c>infra/observability/prometheus/alerts/reporting.yml</c>.
/// </summary>
public static class ReportingAlertCatalog
{
    public const string ServerErrorRateHigh = "ReportingServerErrorRateHigh";

    public const string CachedReadP95High = "ReportingCachedReadP95High";

    public const string UncachedReadP95High = "ReportingUncachedReadP95High";

    public const string ExportFailuresSustained = "ReportingExportFailuresSustained";

    public const string ProjectionFailuresSustained = "ReportingProjectionFailuresSustained";

    public const string ProjectionP95High = "ReportingProjectionP95High";

    public const string CacheMissRateHigh = "ReportingCacheMissRateHigh";

    public static string ServerErrorRatePromQl(double maxPercent = ReportingSlo.MaxServerErrorPercent) =>
        $"100 * sum(rate(reporting_requests_total{{status_class=\"5xx\"}}[5m])) / clamp_min(sum(rate(reporting_requests_total[5m])), 1) > {maxPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static string CachedReadP95PromQl(int maxMs = ReportingSlo.MaxCachedP95LatencyMs) =>
        $"histogram_quantile(0.95, sum(rate(reporting_read_duration_milliseconds_bucket{{cache=\"hit\",outcome=\"success\"}}[5m])) by (le)) > {maxMs}";

    public static string UncachedReadP95PromQl(int maxMs = ReportingSlo.UncachedReportSeconds * 1000) =>
        $"histogram_quantile(0.95, sum(rate(reporting_read_duration_milliseconds_bucket{{cache=\"miss\",outcome=\"success\"}}[5m])) by (le)) > {maxMs}";

    public const string ExportFailuresSustainedPromQl =
        "increase(reporting_export_failures_total[5m]) > 0";

    public const string ProjectionFailuresSustainedPromQl =
        "increase(reporting_messages_failures_total[5m]) > 0";

    public static string ProjectionP95PromQl(int maxMs = 1000) =>
        $"histogram_quantile(0.95, sum(rate(reporting_projection_duration_milliseconds_bucket{{outcome=\"success\"}}[5m])) by (le)) > {maxMs}";

    public const string CacheMissRateHighPromQl =
        "100 * sum(rate(reporting_cache_misses_total[5m])) / clamp_min(sum(rate(reporting_cache_hits_total[5m])) + sum(rate(reporting_cache_misses_total[5m])), 1) > 50 and sum(rate(reporting_read_duration_milliseconds_count[5m])) > 0.1";
}
