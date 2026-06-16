using System.Globalization;
using System.Text;
using CashFlow.Reporting.Application.Abstractions;
using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Infrastructure.Observability;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CashFlow.Reporting.Infrastructure.Exports;

public sealed class ReportExportService(ReportingMetrics metrics) : IReportExportService
{
    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ExportReportResult ExportCsv(DailyReportResult report)
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("ReportDate,TotalDebits,TotalCredits,ConsolidatedBalance,TransactionVolume");
            builder.AppendLine(string.Join(',',
                report.ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                report.TotalDebits.ToString(CultureInfo.InvariantCulture),
                report.TotalCredits.ToString(CultureInfo.InvariantCulture),
                report.ConsolidatedBalance.ToString(CultureInfo.InvariantCulture),
                report.TransactionVolume.ToString(CultureInfo.InvariantCulture)));

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
                FileName = $"daily-report-{report.ReportDate:yyyy-MM-dd}.csv"
            };
        }
        catch (Exception ex)
        {
            metrics.IncrementExportFailure("csv", ex.GetType().Name);
            throw;
        }
    }

    public ExportReportResult ExportPdf(DailyReportResult report)
    {
        try
        {
            var content = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Header().Text($"Daily Consolidated Report — {report.ReportDate:yyyy-MM-dd}")
                        .SemiBold().FontSize(18);
                    page.Content().Column(column =>
                    {
                        column.Spacing(8);
                        column.Item().Text($"Total Debits: {report.TotalDebits:C}");
                        column.Item().Text($"Total Credits: {report.TotalCredits:C}");
                        column.Item().Text($"Consolidated Balance: {report.ConsolidatedBalance:C}");
                        column.Item().Text($"Transaction Volume: {report.TransactionVolume}");
                        column.Item().PaddingTop(12).Text("Chart Metrics").SemiBold();
                        column.Item().Text($"Debits (pie): {report.Dataset.PieSeries.FirstOrDefault(x => x.Label == "Debits")?.Value ?? 0m:C}");
                        column.Item().Text($"Credits (pie): {report.Dataset.PieSeries.FirstOrDefault(x => x.Label == "Credits")?.Value ?? 0m:C}");
                    });
                });
            }).GeneratePdf();

            metrics.IncrementExportSuccess("pdf");
            return new ExportReportResult
            {
                Content = content,
                ContentType = "application/pdf",
                FileName = $"daily-report-{report.ReportDate:yyyy-MM-dd}.pdf"
            };
        }
        catch (Exception ex)
        {
            metrics.IncrementExportFailure("pdf", ex.GetType().Name);
            throw;
        }
    }

    private static void AppendCsvMetrics(StringBuilder builder, string series, IReadOnlyCollection<ChartMetricResult> metrics)
    {
        foreach (var metric in metrics)
        {
            builder.AppendLine(string.Join(',',
                series,
                EscapeCsv(metric.Label),
                metric.Value.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string EscapeCsv(string value) =>
        value.Contains('"') || value.Contains(',')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
