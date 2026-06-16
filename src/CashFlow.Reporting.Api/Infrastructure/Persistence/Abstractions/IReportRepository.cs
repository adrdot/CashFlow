using CashFlow.Reporting.Domain.Entities;

namespace CashFlow.Reporting.Infrastructure.Persistence.Abstractions;

public interface IReportRepository
{
    Task<DailySummary?> GetDailySummaryAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<ReportingTransaction>> ListByDateAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    );
}
