using CashFlow.Reporting.Domain.Entities;
using CashFlow.Reporting.Infrastructure.Caching.Abstractions;
using CashFlow.Reporting.Infrastructure.Persistence.Abstractions;
using CashFlow.Reporting.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Reporting.Infrastructure.Persistence;

public sealed class SqlReportRepository(ReportingDbContext dbContext) : IReportRepository
{
    public async Task<DailySummary?> GetDailySummaryAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    )
    {
        var row = await dbContext
            .DailySummaries.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.ReportDate == reportDate,
                cancellationToken
            );

        return row is null ? null : MapSummary(row);
    }

    public async Task<IReadOnlyCollection<ReportingTransaction>> ListByDateAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    )
    {
        var rows = await dbContext
            .ProjectedTransactions.AsNoTracking()
            .Where(x => x.UserId == userId && x.OccurredOn == reportDate)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToArray();
    }

    private static DailySummary MapSummary(DailySummaryEntity entity) =>
        new()
        {
            UserId = entity.UserId,
            ReportDate = entity.ReportDate,
            TotalDebits = entity.TotalDebits,
            TotalCredits = entity.TotalCredits,
            DebitEntryCount = entity.DebitEntryCount,
            CreditEntryCount = entity.CreditEntryCount,
            TransactionVolume = entity.TransactionVolume,
            LastUpdatedUtc = entity.LastUpdatedUtc,
        };

    private static ReportingTransaction Map(ProjectedTransactionEntity entity) =>
        new()
        {
            Id = entity.Id,
            Type = (ReportTransactionType)entity.Type,
            Amount = entity.Amount,
            Description = entity.Description,
            OccurredOn = entity.OccurredOn,
        };
}

public sealed class TransactionProjectionWriter(
    ReportingDbContext dbContext,
    IReportCache reportCache
)
{
    public async Task ProjectAsync(
        Guid transactionId,
        string userId,
        string type,
        decimal amount,
        string description,
        DateOnly transactionDate,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken
    )
    {
        if (!Enum.TryParse<ReportTransactionType>(type, true, out var transactionType))
        {
            throw new InvalidOperationException($"Unsupported transaction type '{type}'.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );
        try
        {
            dbContext.ProjectedTransactions.Add(
                new ProjectedTransactionEntity
                {
                    Id = transactionId,
                    UserId = userId,
                    Type = (int)transactionType,
                    Amount = amount,
                    Description = description,
                    OccurredOn = transactionDate,
                    CreatedAtUtc = createdAtUtc,
                }
            );

            await dbContext.SaveChangesAsync(cancellationToken);
            await UpsertDailySummaryAsync(
                userId,
                transactionDate,
                transactionType,
                amount,
                cancellationToken
            );
            await transaction.CommitAsync(cancellationToken);
            await reportCache.InvalidateAsync(userId, transactionDate, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateProjection(ex))
        {
            dbContext.ChangeTracker.Clear();
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    private async Task UpsertDailySummaryAsync(
        string userId,
        DateOnly reportDate,
        ReportTransactionType transactionType,
        decimal amount,
        CancellationToken cancellationToken
    )
    {
        var summary = await dbContext.DailySummaries.FirstOrDefaultAsync(
            x => x.UserId == userId && x.ReportDate == reportDate,
            cancellationToken
        );

        if (summary is null)
        {
            dbContext.DailySummaries.Add(
                new DailySummaryEntity
                {
                    UserId = userId,
                    ReportDate = reportDate,
                    TotalDebits = transactionType == ReportTransactionType.Debit ? amount : 0m,
                    TotalCredits = transactionType == ReportTransactionType.Credit ? amount : 0m,
                    DebitEntryCount = transactionType == ReportTransactionType.Debit ? 1 : 0,
                    CreditEntryCount = transactionType == ReportTransactionType.Credit ? 1 : 0,
                    TransactionVolume = 1,
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                }
            );
        }
        else
        {
            if (transactionType == ReportTransactionType.Debit)
            {
                summary.TotalDebits += amount;
                summary.DebitEntryCount++;
            }
            else
            {
                summary.TotalCredits += amount;
                summary.CreditEntryCount++;
            }

            summary.TransactionVolume++;
            summary.LastUpdatedUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDuplicateProjection(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("2601", StringComparison.Ordinal)
            || message.Contains("2627", StringComparison.Ordinal);
    }
}
