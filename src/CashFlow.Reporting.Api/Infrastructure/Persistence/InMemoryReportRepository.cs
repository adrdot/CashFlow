using CashFlow.Reporting.Domain.Entities;
using CashFlow.Reporting.Infrastructure.Persistence.Abstractions;

namespace CashFlow.Reporting.Infrastructure.Persistence;

public sealed class InMemoryReportRepository : IReportRepository
{
    private static readonly (ReportingTransaction Transaction, string UserId)[] SeedTransactions =
    [
        (
            new ReportingTransaction
            {
                Id = Guid.Parse("0f53a50d-a2c4-40f6-8799-8c0d3ad1ce11"),
                Type = ReportTransactionType.Credit,
                Amount = 2500m,
                Description = "Client invoice",
                OccurredOn = new DateOnly(2026, 6, 12),
            },
            "dev-user"
        ),
        (
            new ReportingTransaction
            {
                Id = Guid.Parse("76f5e523-c9fa-4e3d-a586-64f17f0971b7"),
                Type = ReportTransactionType.Debit,
                Amount = 540.75m,
                Description = "Office rent",
                OccurredOn = new DateOnly(2026, 6, 12),
            },
            "dev-user"
        ),
        (
            new ReportingTransaction
            {
                Id = Guid.Parse("af8038bb-feb7-4780-a2f2-39a45ad03316"),
                Type = ReportTransactionType.Debit,
                Amount = 160.25m,
                Description = "Utilities",
                OccurredOn = new DateOnly(2026, 6, 12),
            },
            "dev-user"
        ),
        (
            new ReportingTransaction
            {
                Id = Guid.Parse("76a7596c-b02e-492d-bf16-bce4966c7786"),
                Type = ReportTransactionType.Credit,
                Amount = 875m,
                Description = "Subscription revenue",
                OccurredOn = new DateOnly(2026, 6, 11),
            },
            "dev-user"
        ),
    ];

    public Task<DailySummary?> GetDailySummaryAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var transactions = SeedTransactions
            .Where(entry => entry.UserId == userId && entry.Transaction.OccurredOn == reportDate)
            .Select(entry => entry.Transaction)
            .ToArray();

        if (transactions.Length == 0)
        {
            return Task.FromResult<DailySummary?>(null);
        }

        var debits = transactions.Where(x => x.Type == ReportTransactionType.Debit).ToArray();
        var credits = transactions.Where(x => x.Type == ReportTransactionType.Credit).ToArray();

        return Task.FromResult<DailySummary?>(
            new DailySummary
            {
                UserId = userId,
                ReportDate = reportDate,
                TotalDebits = debits.Sum(x => x.Amount),
                TotalCredits = credits.Sum(x => x.Amount),
                DebitEntryCount = debits.Length,
                CreditEntryCount = credits.Length,
                TransactionVolume = transactions.Length,
                LastUpdatedUtc = DateTimeOffset.UtcNow,
            }
        );
    }

    public Task<IReadOnlyCollection<ReportingTransaction>> ListByDateAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var matches = SeedTransactions
            .Where(entry => entry.UserId == userId && entry.Transaction.OccurredOn == reportDate)
            .Select(entry => entry.Transaction)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ReportingTransaction>>(matches);
    }
}
