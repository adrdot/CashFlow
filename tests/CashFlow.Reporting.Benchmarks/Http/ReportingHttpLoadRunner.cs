using CashFlow.Reporting.Api.Observability;
using CashFlow.Reporting.Benchmarks.Http;
using NBomber.Contracts;
using NBomber.CSharp;

namespace CashFlow.Reporting.Benchmarks.Http;

internal sealed record ReportLoadStepResult(
    int TargetRate,
    long OkCount,
    long FailCount,
    double OkRps,
    double MeanLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double FailPercent,
    string? TopFailStatusCode)
{
    public long TotalCount => OkCount + FailCount;
}

internal static class ReportingLoadScenario
{
    public static ReportLoadStepResult RunStep(
        HttpClient client,
        int targetRate,
        int durationSeconds,
        DateOnly reportDate,
        bool nbomberWarmup = false)
    {
        var warmupDuration = nbomberWarmup
            ? TimeSpan.FromSeconds(Math.Min(3, Math.Max(1, durationSeconds / 3)))
            : TimeSpan.FromMilliseconds(100);

        var query = $"?date={reportDate:yyyy-MM-dd}";

        var scenario = Scenario.Create($"get_daily_report_rps_{targetRate}", async _ =>
        {
            var response = await client.GetAsync($"/api/reports/daily{query}");

            return response.IsSuccessStatusCode
                ? Response.Ok(statusCode: ((int)response.StatusCode).ToString())
                : Response.Fail(statusCode: ((int)response.StatusCode).ToString());
        })
        .WithWarmUpDuration(warmupDuration)
        .WithLoadSimulations(
            Simulation.Inject(
                rate: targetRate,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(durationSeconds)));

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        var scenarioStats = stats.ScenarioStats.FirstOrDefault()
            ?? throw new InvalidOperationException("No scenario stats were produced by the load test.");

        var okCount = scenarioStats.Ok.Request.Count;
        var failCount = scenarioStats.Fail.Request.Count;
        var total = okCount + failCount;
        var failPercent = total == 0 ? 100d : failCount * 100d / total;

        var topFailStatus = scenarioStats.Fail.StatusCodes
            .OrderByDescending(entry => entry.Count)
            .Select(entry => entry.StatusCode)
            .FirstOrDefault();

        return new ReportLoadStepResult(
            TargetRate: targetRate,
            OkCount: okCount,
            FailCount: failCount,
            OkRps: scenarioStats.Ok.Request.RPS,
            MeanLatencyMs: scenarioStats.Ok.Latency.MeanMs,
            P50LatencyMs: scenarioStats.Ok.Latency.Percent50,
            P95LatencyMs: scenarioStats.Ok.Latency.Percent95,
            FailPercent: failPercent,
            TopFailStatusCode: topFailStatus);
    }

    public static async Task PrimeReportCacheAsync(
        HttpClient client,
        DateOnly reportDate,
        int sequentialRequests = 30,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Priming report cache ({sequentialRequests} sequential GETs)...");
        var query = $"?date={reportDate:yyyy-MM-dd}";

        for (var i = 0; i < sequentialRequests; i++)
        {
            using var response = await client.GetAsync($"/api/reports/daily{query}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Cache priming failed ({(int)response.StatusCode}): {body}");
            }
        }

        Console.WriteLine("Report cache primed.");
    }
}

internal static class ReportingHttpLoadRunner
{
    public static async Task RunAsync(string[] args)
    {
        var options = await ReportingLoadTestOptions.ParseAsync(args);

        await using var clientHolder = ReportingLoadHttpClient.Create(options.BaseUrl, options.BearerToken);
        var reportDate = options.ReportDate ?? new DateOnly(2026, 6, 12);

        await ReportingLoadScenario.PrimeReportCacheAsync(clientHolder.Client, reportDate);

        var result = ReportingLoadScenario.RunStep(
            clientHolder.Client,
            options.Rate,
            options.DurationSeconds,
            reportDate,
            nbomberWarmup: false);

        Console.WriteLine();
        Console.WriteLine("=== Reporting HTTP Load Summary ===");
        Console.WriteLine($"Target: {(options.BaseUrl ?? "in-memory WebApplicationFactory")}");
        Console.WriteLine($"Auth: {options.DescribeAuth()}");
        Console.WriteLine($"Report date: {reportDate:yyyy-MM-dd}");
        Console.WriteLine(
            $"Target RPS: {options.Rate} | Duration: {options.DurationSeconds}s | Max failure: {ReportingSlo.MaxFailurePercent}%");
        Console.WriteLine(
            $"Latency gates: p50 < {ReportingSlo.MaxCachedP50LatencyMs}ms | p95 < {ReportingSlo.MaxCachedP95LatencyMs}ms (cached steady state)");
        Console.WriteLine(
            $"OK: {result.OkCount} | Fail: {result.FailCount} | Fail %: {result.FailPercent:F2} | RPS: {result.OkRps:F1}");
        Console.WriteLine(
            $"Latency ms — mean: {result.MeanLatencyMs:F1} | p50: {result.P50LatencyMs:F1} | p95: {result.P95LatencyMs:F1}");

        if (result.FailCount > 0 && result.TopFailStatusCode == "429")
        {
            Console.WriteLine(
                "Hint: HTTP 429 indicates rate limiting. Disable Security:RateLimitingEnabled on reporting-api and restart AppHost.");
        }

        if (result.FailPercent > ReportingSlo.MaxFailurePercent)
        {
            var statusHint = string.IsNullOrWhiteSpace(result.TopFailStatusCode)
                ? string.Empty
                : $" Top failure status: {result.TopFailStatusCode}.";
            throw new InvalidOperationException(
                $"Failure rate {result.FailPercent:F2}% exceeded target of {ReportingSlo.MaxFailurePercent}%.{statusHint}");
        }

        if (result.P50LatencyMs > ReportingSlo.MaxCachedP50LatencyMs)
        {
            throw new InvalidOperationException(
                $"Cached read p50 {result.P50LatencyMs:F1}ms exceeded target of {ReportingSlo.MaxCachedP50LatencyMs}ms.");
        }

        if (result.P95LatencyMs > ReportingSlo.MaxCachedP95LatencyMs)
        {
            throw new InvalidOperationException(
                $"Cached read p95 {result.P95LatencyMs:F1}ms exceeded target of {ReportingSlo.MaxCachedP95LatencyMs}ms.");
        }
    }
}
