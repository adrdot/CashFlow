using System.Globalization;
using System.Text;
using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Infrastructure.Exports.Abstractions;
using CashFlow.Reporting.Infrastructure.Observability;

namespace CashFlow.Reporting.Infrastructure.Exports;

public sealed class CsvReportExportService(ReportingMetrics metrics) : ICsvReportExporter
{
    public ExportReportResult Export(DailyReportResult report)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "ReportDate,TotalDebits,TotalCredits,ConsolidatedBalance,TransactionVolume"
            );
            builder.AppendLine(
                string.Join(
                    ',',
                    report.ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    report.TotalDebits.ToString(CultureInfo.InvariantCulture),
                    report.TotalCredits.ToString(CultureInfo.InvariantCulture),
                    report.ConsolidatedBalance.ToString(CultureInfo.InvariantCulture),
                    report.TransactionVolume.ToString(CultureInfo.InvariantCulture)
                )
            );

            builder.AppendLine();
            builder.AppendLine("ChartSeries,Label,Value");
            AppendCsvMetrics(builder, "Line", report.Dataset.LineSeries);
            AppendCsvMetrics(builder, "Bar", report.Dataset.BarSeries);
            AppendCsvMetrics(builder, "Pie", report.Dataset.PieSeries);

            metrics.IncrementExportSuccess("csv");
            return new ExportReportResult
            {
                Content = Encoding.UTF8.GetBytes(builder.ToString()),
                ContentType = "text/csv",
                FileName = $"daily-report-{report.ReportDate:yyyy-MM-dd}.csv",
            };
        }
        catch (Exception ex)
        {
            metrics.IncrementExportFailure("csv", ex.GetType().Name);
            throw;
        }
    }

    private static void AppendCsvMetrics(
        StringBuilder builder,
        string series,
        IReadOnlyCollection<ChartMetricResult> metrics
    )
    {
        foreach (var metric in metrics)
        {
            builder.AppendLine(
                string.Join(
                    ',',
                    series,
                    EscapeCsv(metric.Label),
                    metric.Value.ToString(CultureInfo.InvariantCulture)
                )
            );
        }
    }

    private static string EscapeCsv(string value) =>
        value.Contains('"') || value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
