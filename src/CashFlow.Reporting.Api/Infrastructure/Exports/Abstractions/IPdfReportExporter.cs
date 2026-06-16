using CashFlow.Reporting.Application.Contracts;

namespace CashFlow.Reporting.Infrastructure.Exports.Abstractions;

public interface IPdfReportExporter
{
    ExportReportResult Export(DailyReportResult report);
}
