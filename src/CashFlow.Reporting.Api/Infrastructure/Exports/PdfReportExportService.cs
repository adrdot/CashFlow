using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Infrastructure.Exports.Abstractions;
using CashFlow.Reporting.Infrastructure.Observability;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CashFlow.Reporting.Infrastructure.Exports;

public sealed class PdfReportExportService(ReportingMetrics metrics) : IPdfReportExporter
{
    static PdfReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ExportReportResult Export(DailyReportResult report)
    {
        try
        {
            var content = Document
                .Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(40);
                        page.Header()
                            .Text($"Daily Consolidated Report — {report.ReportDate:yyyy-MM-dd}")
                            .SemiBold()
                            .FontSize(18);
                        page.Content()
                            .Column(column =>
                            {
                                column.Spacing(8);
                                column.Item().Text($"Total Debits: {report.TotalDebits:C}");
                                column.Item().Text($"Total Credits: {report.TotalCredits:C}");
                                column
                                    .Item()
                                    .Text($"Consolidated Balance: {report.ConsolidatedBalance:C}");
                                column
                                    .Item()
                                    .Text($"Transaction Volume: {report.TransactionVolume}");
                                column.Item().PaddingTop(12).Text("Chart Metrics").SemiBold();
                                column
                                    .Item()
                                    .Text(
                                        $"Debits (pie): {report.Dataset.PieSeries.FirstOrDefault(x => x.Label == "Debits")?.Value ?? 0m:C}"
                                    );
                                column
                                    .Item()
                                    .Text(
                                        $"Credits (pie): {report.Dataset.PieSeries.FirstOrDefault(x => x.Label == "Credits")?.Value ?? 0m:C}"
                                    );
                            });
                    });
                })
                .GeneratePdf();

            metrics.IncrementExportSuccess("pdf");
            return new ExportReportResult
            {
                Content = content,
                ContentType = "application/pdf",
                FileName = $"daily-report-{report.ReportDate:yyyy-MM-dd}.pdf",
            };
        }
        catch (Exception ex)
        {
            metrics.IncrementExportFailure("pdf", ex.GetType().Name);
            throw;
        }
    }
}
