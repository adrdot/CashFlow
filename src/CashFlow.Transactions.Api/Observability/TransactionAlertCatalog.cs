namespace CashFlow.Transactions.Api.Observability;

/// <summary>
/// Operational alert identifiers and PromQL expressions for the Transactions API.
/// Mirrors <c>infra/observability/prometheus/alerts/transactions.yml</c>.
/// </summary>
public static class TransactionAlertCatalog
{
    public const string PersistenceP95High = "TransactionsPersistenceP95High";

    public const string EndToEndP95High = "TransactionsEndToEndP95High";

    public const string ServerErrorRateHigh = "TransactionsServerErrorRateHigh";

    public const string PublishFailureRateHigh = "TransactionsPublishFailureRateHigh";

    public const string PublishFailuresSustained = "TransactionsPublishFailuresSustained";

    public const string PersistenceFailuresDetected = "TransactionsPersistenceFailuresDetected";

    public static string PersistenceP95PromQl(int maxMs = TransactionSlo.MaxPersistenceLatencyMs) =>
        $"histogram_quantile(0.95, sum(rate(transactions_persistence_duration_milliseconds_bucket{{outcome=\"success\"}}[5m])) by (le)) > {maxMs}";

    public static string EndToEndP95PromQl(int maxSeconds = TransactionSlo.EndToEndRecordingSeconds) =>
        $"histogram_quantile(0.95, sum(rate(transactions_end_to_end_duration_milliseconds_bucket[5m])) by (le)) > {maxSeconds * 1000}";

    public static string ServerErrorRatePromQl(double maxPercent = TransactionSlo.MaxServerErrorPercent) =>
        $"100 * sum(rate(transactions_requests_total{{status_class=\"5xx\"}}[5m])) / sum(rate(transactions_requests_total[5m])) > {maxPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static string PublishFailureRatePromQl =>
        "100 * sum(rate(transactions_events_publish_failures_total[5m])) / clamp_min(sum(rate(transactions_events_published_total[5m])) + sum(rate(transactions_events_publish_failures_total[5m])), 1) > 1";

    public const string PublishFailuresSustainedPromQl =
        "increase(transactions_events_publish_failures_total[5m]) > 0";

    public const string PersistenceFailuresPromQl = "increase(transactions_persistence_failures_total[5m]) > 0";
}
