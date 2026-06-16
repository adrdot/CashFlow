using CashFlow.Reporting.Application.Contracts;

namespace CashFlow.Reporting.Application.Abstractions;

public interface IReportCache
{
    Task<DailyReportResult?> GetAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string userId,
        DateOnly reportDate,
        DailyReportResult report,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default);
}
