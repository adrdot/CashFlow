using System.Text.Json;
using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Infrastructure.Caching.Abstractions;
using CashFlow.Reporting.Infrastructure.Observability;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace CashFlow.Reporting.Infrastructure.Caching;

public sealed class NullReportCache : IReportCache
{
    public Task<DailyReportResult?> GetAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<DailyReportResult?>(null);

    public Task SetAsync(
        string userId,
        DateOnly reportDate,
        DailyReportResult report,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;

    public Task InvalidateAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;
}

public sealed class RedisReportCache(
    IDistributedCache cache,
    IOptions<ReportingCacheOptions> cacheOptions,
    ReportingMetrics metrics
) : IReportCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DailyReportResult?> GetAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    )
    {
        var payload = await cache.GetStringAsync(BuildKey(userId, reportDate), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            metrics.IncrementCacheMiss();
            return null;
        }

        metrics.IncrementCacheHit();
        return JsonSerializer.Deserialize<DailyReportResult>(payload, JsonOptions);
    }

    public async Task SetAsync(
        string userId,
        DateOnly reportDate,
        DailyReportResult report,
        CancellationToken cancellationToken = default
    )
    {
        var payload = JsonSerializer.Serialize(report with { FromCache = false }, JsonOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ResolveTtl(reportDate, cacheOptions.Value),
        };

        await cache.SetStringAsync(
            BuildKey(userId, reportDate),
            payload,
            options,
            cancellationToken
        );
    }

    public async Task InvalidateAsync(
        string userId,
        DateOnly reportDate,
        CancellationToken cancellationToken = default
    )
    {
        await cache.RemoveAsync(BuildKey(userId, reportDate), cancellationToken);
        metrics.IncrementCacheInvalidation();
    }

    private static string BuildKey(string userId, DateOnly reportDate) =>
        $"report:{userId}:{reportDate:yyyy-MM-dd}";

    private static TimeSpan ResolveTtl(DateOnly reportDate, ReportingCacheOptions options)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return reportDate == today
            ? TimeSpan.FromMinutes(options.CurrentDayTtlMinutes)
            : TimeSpan.FromHours(options.ClosedDayTtlHours);
    }
}
