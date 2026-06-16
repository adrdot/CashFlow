using CashFlow.Reporting.Api.Observability;

namespace CashFlow.Reporting.UnitTests.Observability;

public sealed class ReportingAlertCatalogTests
{
    [Fact]
    public void PromQlHelpers_ContainExpectedMetricNames()
    {
        Assert.Contains("reporting_requests_total", ReportingAlertCatalog.ServerErrorRatePromQl());
        Assert.Contains("reporting_read_duration_milliseconds", ReportingAlertCatalog.CachedReadP95PromQl());
        Assert.Contains("reporting_read_duration_milliseconds", ReportingAlertCatalog.UncachedReadP95PromQl());
        Assert.Contains("reporting_export_failures_total", ReportingAlertCatalog.ExportFailuresSustainedPromQl);
        Assert.Contains("reporting_messages_failures_total", ReportingAlertCatalog.ProjectionFailuresSustainedPromQl);
        Assert.Contains("reporting_projection_duration_milliseconds", ReportingAlertCatalog.ProjectionP95PromQl());
        Assert.Contains("reporting_cache_misses_total", ReportingAlertCatalog.CacheMissRateHighPromQl);
    }
}
