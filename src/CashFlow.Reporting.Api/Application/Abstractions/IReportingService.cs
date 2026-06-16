using CashFlow.Reporting.Application.Contracts;

namespace CashFlow.Reporting.Application.Abstractions;

public interface IReportingService
{
    Task<DailyReportResult> GetDailyReportAsync(
        GetDailyReportRequest request,
        CancellationToken cancellationToken = default
    );
}
