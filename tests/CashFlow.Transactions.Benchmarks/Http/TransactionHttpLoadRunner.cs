namespace CashFlow.Transactions.Benchmarks.Http;

internal static class TransactionHttpLoadRunner
{
    public static async Task RunAsync(string[] args)
    {
        var options = await LoadTestOptions.ParseAsync(args);

        await using var clientHolder = TransactionsLoadHttpClient.Create(
            options.BaseUrl,
            options.BearerToken
        );
        var payload = TransactionLoadScenario.CreatePayload();

        var result = TransactionLoadScenario.RunStep(
            clientHolder.Client,
            payload,
            options.Rate,
            options.DurationSeconds,
            warmup: true
        );

        Console.WriteLine();
        Console.WriteLine("=== Transaction HTTP Load Summary ===");
        Console.WriteLine($"Target: {(options.BaseUrl ?? "in-memory WebApplicationFactory")}");
        Console.WriteLine($"Auth: {options.DescribeAuth()}");
        Console.WriteLine(
            $"Target RPS: {options.Rate} | Duration: {options.DurationSeconds}s | Persistence mean latency gate: < {TransactionLoadTestSloGates.MaxMeanLatencyMs}ms"
        );
        Console.WriteLine(
            $"OK: {result.OkCount} | Fail: {result.FailCount} | RPS: {result.OkRps:F1} | Mean latency: {result.MeanLatencyMs:F1}ms"
        );

        if (result.FailCount > 0)
        {
            throw new InvalidOperationException(
                $"HTTP load test had {result.FailCount} failed requests."
            );
        }

        if (result.MeanLatencyMs > TransactionLoadTestSloGates.MaxMeanLatencyMs)
        {
            throw new InvalidOperationException(
                $"Mean latency {result.MeanLatencyMs:F1}ms exceeded persistence target of {TransactionLoadTestSloGates.MaxMeanLatencyMs}ms."
            );
        }
    }
}
