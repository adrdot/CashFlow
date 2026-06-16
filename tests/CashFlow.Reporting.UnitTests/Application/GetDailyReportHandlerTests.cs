using System.Diagnostics.Metrics;
using CashFlow.Reporting.Application;
using CashFlow.Reporting.Application.Abstractions;
using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Application.UseCases;
using CashFlow.Reporting.Domain.Entities;
using CashFlow.Reporting.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Reporting.UnitTests.Application;

public sealed class GetDailyReportHandlerTests
{
    [Fact]
    public async Task GetDailyReportAsync_ComputesTotalsAndDataset_WhenSummaryExists()
    {
        var repository = new FakeReportRepository(new DailySummary
        {
            UserId = "user-1",
            ReportDate = new DateOnly(2026, 6, 12),
            TotalDebits = 35m,
            TotalCredits = 100m,
            DebitEntryCount = 1,
            CreditEntryCount = 1,
            TransactionVolume = 2,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        var handler = CreateHandler(repository, new FakeReportCache());

        var result = await handler.GetDailyReportAsync(new GetDailyReportRequest
        {
            UserId = "user-1",
            ReportDate = new DateOnly(2026, 6, 12)
        });

        Assert.True(result.HasData);
        Assert.Equal(35m, result.TotalDebits);
        Assert.Equal(100m, result.TotalCredits);
        Assert.Equal(65m, result.ConsolidatedBalance);
        Assert.Equal(2, result.TransactionVolume);
        Assert.Equal(3, result.Dataset.LineSeries.Count);
        Assert.Equal(3, result.Dataset.BarSeries.Count);
        Assert.Equal(2, result.Dataset.PieSeries.Count);
    }

    [Fact]
    public async Task GetDailyReportAsync_ReturnsZeroState_WhenNoSummaryExists()
    {
        var repository = new FakeReportRepository(null);
        var handler = CreateHandler(repository, new FakeReportCache());

        var result = await handler.GetDailyReportAsync(new GetDailyReportRequest
        {
            UserId = "user-1",
            ReportDate = new DateOnly(2026, 6, 10)
        });

        Assert.False(result.HasData);
        Assert.Equal(0m, result.TotalDebits);
        Assert.Equal(0m, result.TotalCredits);
        Assert.Equal(0m, result.ConsolidatedBalance);
        Assert.Equal(0, result.TransactionVolume);
    }

    [Fact]
    public async Task GetDailyReportAsync_ReturnsCachedResult_WhenCacheHit()
    {
        var cached = DailyReportBuilder.Build(new DateOnly(2026, 6, 12), new DailySummary
        {
            UserId = "user-1",
            ReportDate = new DateOnly(2026, 6, 12),
            TotalCredits = 10m,
            TotalDebits = 0m,
            CreditEntryCount = 1,
            TransactionVolume = 1,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        var cache = new FakeReportCache(cached);
        var handler = CreateHandler(new FakeReportRepository(null), cache);

        var result = await handler.GetDailyReportAsync(new GetDailyReportRequest
        {
            UserId = "user-1",
            ReportDate = new DateOnly(2026, 6, 12)
        });

        Assert.True(result.FromCache);
        Assert.Equal(10m, result.TotalCredits);
    }

    private static GetDailyReportHandler CreateHandler(IReportRepository repository, IReportCache cache)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<ReportingQueueStats>();
        var provider = services.BuildServiceProvider();
        var metrics = new ReportingMetrics(
            provider.GetRequiredService<IMeterFactory>(),
            provider.GetRequiredService<ReportingQueueStats>());
        return new GetDailyReportHandler(repository, cache, metrics);
    }

    private sealed class FakeReportRepository(DailySummary? summary) : IReportRepository
    {
        public Task<DailySummary?> GetDailySummaryAsync(
            string userId,
            DateOnly reportDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(summary is not null && summary.UserId == userId && summary.ReportDate == reportDate
                ? summary
                : null);

        public Task<IReadOnlyCollection<ReportingTransaction>> ListByDateAsync(
            string userId,
            DateOnly reportDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ReportingTransaction>>([]);
    }

    private sealed class FakeReportCache(DailyReportResult? seeded = null) : IReportCache
    {
        public Task<DailyReportResult?> GetAsync(
            string userId,
            DateOnly reportDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(seeded);

        public Task SetAsync(
            string userId,
            DateOnly reportDate,
            DailyReportResult report,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateAsync(
            string userId,
            DateOnly reportDate,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
