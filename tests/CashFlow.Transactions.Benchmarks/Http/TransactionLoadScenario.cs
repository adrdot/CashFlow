using System.Text;
using System.Text.Json;
using CashFlow.Transactions.Application.Contracts;
using NBomber.Contracts;
using NBomber.CSharp;

namespace CashFlow.Transactions.Benchmarks.Http;

internal sealed record LoadStepResult(
    int TargetRate,
    long OkCount,
    long FailCount,
    double OkRps,
    double MeanLatencyMs,
    double FailPercent,
    string? TopFailStatusCode
)
{
    public long TotalCount => OkCount + FailCount;
}

internal static class TransactionLoadScenario
{
    public static string CreatePayload() =>
        JsonSerializer.Serialize(
            new CreateTransactionRequest
            {
                Type = "Credit",
                Amount = 10.50m,
                Description = "HTTP load benchmark",
                TransactionDate = new DateOnly(2026, 6, 14),
            }
        );

    public static LoadStepResult RunStep(
        HttpClient client,
        string payload,
        int targetRate,
        int durationSeconds,
        bool warmup = false
    )
    {
        var warmupDuration = warmup
            ? TimeSpan.FromSeconds(Math.Min(3, Math.Max(1, durationSeconds / 3)))
            : TimeSpan.FromMilliseconds(100);

        var scenario = Scenario
            .Create(
                $"create_transaction_rps_{targetRate}",
                async _ =>
                {
                    using var content = new StringContent(
                        payload,
                        Encoding.UTF8,
                        "application/json"
                    );
                    var response = await client.PostAsync("/api/transactions", content);

                    return response.IsSuccessStatusCode
                        ? Response.Ok(
                            statusCode: ((int)response.StatusCode).ToString(),
                            sizeBytes: payload.Length
                        )
                        : Response.Fail(
                            statusCode: ((int)response.StatusCode).ToString(),
                            sizeBytes: payload.Length
                        );
                }
            )
            .WithWarmUpDuration(warmupDuration)
            .WithLoadSimulations(
                Simulation.Inject(
                    rate: targetRate,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(durationSeconds)
                )
            );

        var stats = NBomberRunner.RegisterScenarios(scenario).Run();

        var scenarioStats =
            stats.ScenarioStats.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No scenario stats were produced by the load test."
            );

        var okCount = scenarioStats.Ok.Request.Count;
        var failCount = scenarioStats.Fail.Request.Count;
        var total = okCount + failCount;
        var failPercent = total == 0 ? 100d : failCount * 100d / total;

        var topFailStatus = scenarioStats
            .Fail.StatusCodes.OrderByDescending(entry => entry.Count)
            .Select(entry => entry.StatusCode)
            .FirstOrDefault();

        return new LoadStepResult(
            TargetRate: targetRate,
            OkCount: okCount,
            FailCount: failCount,
            OkRps: scenarioStats.Ok.Request.RPS,
            MeanLatencyMs: scenarioStats.Ok.Latency.MeanMs,
            FailPercent: failPercent,
            TopFailStatusCode: topFailStatus
        );
    }
}
