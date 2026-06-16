using System.Net;
using CashFlow.Reporting.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Reporting.ContractTests;

public sealed class MetricsEndpointContractTests
{
    [Fact]
    public async Task MetricsEndpoint_ExposesReportingInstruments()
    {
        await using var factory = new ReportingWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var metrics = scope.ServiceProvider.GetRequiredService<ReportingMetrics>();
        metrics.IncrementMessagesConsumed();
        metrics.RecordProjectionDuration(TimeSpan.FromMilliseconds(1), "success");
        metrics.RecordHttpRequest("GET", "/api/reports/daily", 200);
        metrics.RecordDailyReadDuration(TimeSpan.FromMilliseconds(5), fromCache: true, "success");

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("reporting_messages_consumed_total", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reporting_projection_duration_milliseconds", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reporting_sqs_visible_messages", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reporting_requests_total", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reporting_read_duration_milliseconds", body, StringComparison.OrdinalIgnoreCase);
    }
}
