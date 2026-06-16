namespace Aspire.CashFlow.ServiceDefaults.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// Exposes <c>/metrics</c> for Prometheus scraping. Disable in environments that export only via OTLP.
    /// </summary>
    public bool PrometheusEnabled { get; set; } = true;
}
