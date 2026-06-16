using CashFlow.Reporting.Application.Contracts;

namespace CashFlow.Reporting.Infrastructure.Exports.Abstractions;

public interface ICsvReportExporter
{
    ExportReportResult Export(DailyReportResult report);
}
