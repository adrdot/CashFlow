using CashFlow.Reporting.Application.Contracts;

namespace CashFlow.Reporting.Application.Abstractions;

public interface IReportExportService
{
    ExportReportResult ExportCsv(DailyReportResult report);

    ExportReportResult ExportPdf(DailyReportResult report);
}
