using CashFlow.Reporting.Application;
using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Domain.Entities;
using CashFlow.Reporting.Infrastructure.Exports;
using CashFlow.Reporting.Infrastructure.Observability;

namespace CashFlow.Reporting.UnitTests.Application;

public sealed class DailyReportBuilderTests
{
    [Fact]
    public void Build_ComputesBalanceFromSummary()
    {
        var result = DailyReportBuilder.Build(new DateOnly(2026, 6, 12), new DailySummary
        {
            UserId = "user-1",
            ReportDate = new DateOnly(2026, 6, 12),
            TotalDebits = 40m,
            TotalCredits = 100m,
            DebitEntryCount = 2,
            CreditEntryCount = 1,
            TransactionVolume = 3,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        Assert.Equal(60m, result.ConsolidatedBalance);
        Assert.Equal(2m, result.Dataset.BarSeries.First(x => x.Label == "Debit Entries").Value);
    }
}

public sealed class ReportExportServiceTests
{
    [Fact]
    public void ExportCsv_MatchesDisplayedTotals()
    {
        var report = DailyReportBuilder.Build(new DateOnly(2026, 6, 12), new DailySummary
        {
            UserId = "user-1",
            ReportDate = new DateOnly(2026, 6, 12),
            TotalDebits = 10m,
            TotalCredits = 25m,
            DebitEntryCount = 1,
            CreditEntryCount = 1,
            TransactionVolume = 2,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        var service = new ReportExportService(new ReportingMetrics(new FakeMeterFactory(), new ReportingQueueStats()));
        var export = service.ExportCsv(report);
        var csv = System.Text.Encoding.UTF8.GetString(export.Content);

        Assert.Contains("25", csv);
        Assert.Contains("10", csv);
        Assert.Contains("15", csv);
    }

    private sealed class FakeMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) =>
            new(options.Name ?? "test");

        public System.Diagnostics.Metrics.Meter Create(string name, string? version = null, IEnumerable<KeyValuePair<string, object?>>? tags = null) =>
            new(name, version, tags);

        public void Dispose()
        {
        }
    }
}
