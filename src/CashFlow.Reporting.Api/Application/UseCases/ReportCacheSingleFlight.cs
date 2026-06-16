using System.Collections.Concurrent;
using CashFlow.Reporting.Application.Contracts;

namespace CashFlow.Reporting.Application.UseCases;

internal static class ReportCacheSingleFlight
{
    private static readonly ConcurrentDictionary<string, Task<DailyReportResult>> InFlight =
        new(StringComparer.Ordinal);

    public static Task<DailyReportResult> GetOrLoadAsync(
        string userId,
        DateOnly reportDate,
        Func<CancellationToken, Task<DailyReportResult>> factory,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(userId, reportDate);

        while (true)
        {
            if (InFlight.TryGetValue(key, out var existing))
            {
                return AwaitExistingAsync(existing, cancellationToken);
            }

            var loadTask = RunLoadAsync(key, factory, cancellationToken);
            if (InFlight.TryAdd(key, loadTask))
            {
                return loadTask;
            }
        }
    }

    private static async Task<DailyReportResult> RunLoadAsync(
        string key,
        Func<CancellationToken, Task<DailyReportResult>> factory,
        CancellationToken cancellationToken)
    {
        try
        {
            return await factory(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            InFlight.TryRemove(key, out _);
        }
    }

    private static async Task<DailyReportResult> AwaitExistingAsync(
        Task<DailyReportResult> existing,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
        {
            return await existing.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await existing.ConfigureAwait(false);
    }

    private static string BuildKey(string userId, DateOnly reportDate) =>
        $"{userId}:{reportDate:yyyy-MM-dd}";
}
