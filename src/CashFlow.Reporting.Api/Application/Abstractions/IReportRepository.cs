using CashFlow.Reporting.Domain.Entities;

namespace CashFlow.Reporting.Application.Abstractions;

public interface IReportRepository
{
    Task<DailySummary?> GetDailySummaryAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReportingTransaction>> ListByDateAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default);
}
