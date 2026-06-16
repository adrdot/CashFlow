using System.Diagnostics;
using CashFlow.Reporting.Application.Abstractions;
using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Infrastructure.Observability;

namespace CashFlow.Reporting.Application.UseCases;

public sealed class GetDailyReportHandler(
    IReportRepository reportRepository,
    IReportCache reportCache,
    ReportingMetrics metrics) : IReportingService
{
    public async Task<DailyReportResult> GetDailyReportAsync(
        GetDailyReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserId);

        var reportDate = request.ReportDate ?? DateOnly.FromDateTime(DateTime.Today);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var cached = await reportCache.GetAsync(request.UserId, reportDate, cancellationToken);
            if (cached is not null)
            {
                var hit = cached with { FromCache = true };
                metrics.RecordDailyReadDuration(stopwatch.Elapsed, fromCache: true, "success");
                return hit;
            }

            var result = await ReportCacheSingleFlight.GetOrLoadAsync(
                request.UserId,
                reportDate,
                async ct =>
                {
                    cached = await reportCache.GetAsync(request.UserId, reportDate, ct);
                    if (cached is not null)
                    {
                        return cached with { FromCache = true };
                    }

                    var summary = await reportRepository.GetDailySummaryAsync(request.UserId, reportDate, ct);
                    var built = DailyReportBuilder.Build(reportDate, summary);
                    await reportCache.SetAsync(request.UserId, reportDate, built, ct);
                    return built;
                },
                cancellationToken);

            metrics.RecordDailyReadDuration(stopwatch.Elapsed, result.FromCache, "success");
            return result;
        }
        catch
        {
            metrics.RecordDailyReadDuration(stopwatch.Elapsed, fromCache: false, "failure");
            throw;
        }
    }
}
