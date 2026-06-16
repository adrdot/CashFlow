namespace CashFlow.Transactions.Benchmarks.Http;

internal static class TransactionHttpStressRunner
{
    public static async Task RunAsync(string[] args)
    {
        var options = await StressTestOptions.ParseAsync(args);

        if (options.StartRate <= 0 || options.StepRate <= 0 || options.MaxRate <= 0 || options.StepSeconds <= 0)
        {
            throw new ArgumentException("start-rate, step, max-rate and step-duration must be greater than zero.");
        }

        if (options.StartRate > options.MaxRate)
        {
            throw new ArgumentException("start-rate cannot be greater than max-rate.");
        }

        await using var clientHolder = TransactionsLoadHttpClient.Create(options.BaseUrl, options.BearerToken);
        var payload = TransactionLoadScenario.CreatePayload();

        Console.WriteLine("=== Transaction HTTP Stress Test ===");
        Console.WriteLine($"Target: {(options.BaseUrl ?? "in-memory WebApplicationFactory")}");
        Console.WriteLine($"Auth: {options.DescribeAuth()}");
        Console.WriteLine(
            $"Steps: {options.StartRate} -> {options.MaxRate} RPS (+{options.StepRate}/step, {options.StepSeconds}s each)");
        Console.WriteLine(
            options.StepPauseSeconds > 0
                ? $"Pause between steps: {options.StepPauseSeconds}s (resets API rate-limit window)"
                : "Pause between steps: disabled");
        Console.WriteLine(
            $"Failure when fail rate >= {options.FailureThresholdPercent:F1}% or mean latency > {options.MaxMeanLatencyMs}ms");
        Console.WriteLine();

        var stepResults = new List<LoadStepResult>();
        int? lastSustainableRate = null;
        LoadStepResult? breakpoint = null;

        for (var rate = options.StartRate; rate <= options.MaxRate; rate += options.StepRate)
        {
            var result = TransactionLoadScenario.RunStep(
                clientHolder.Client,
                payload,
                rate,
                options.StepSeconds,
                warmup: rate == options.StartRate);

            stepResults.Add(result);
            PrintStepLine(result);

            if (IsBreakpoint(result, options))
            {
                breakpoint = result;
                break;
            }

            lastSustainableRate = rate;

            if (options.StepPauseSeconds > 0 && rate + options.StepRate <= options.MaxRate)
            {
                await WaitBetweenStepsAsync(options.StepPauseSeconds);
            }
        }

        PrintSummary(options, lastSustainableRate, breakpoint, stepResults);
    }

    private static bool IsBreakpoint(LoadStepResult result, StressTestOptions options)
    {
        if (result.TotalCount == 0)
        {
            return true;
        }

        if (result.FailPercent >= options.FailureThresholdPercent)
        {
            return true;
        }

        return result.OkCount > 0 && result.MeanLatencyMs > options.MaxMeanLatencyMs;
    }

    private static void PrintStepLine(LoadStepResult result)
    {
        Console.WriteLine(
            $"RPS {result.TargetRate,4}: OK {result.OkCount,5} | Fail {result.FailCount,5} " +
            $"( {result.FailPercent,5:F1}%) | Achieved {result.OkRps,6:F1} rps | Mean {result.MeanLatencyMs,7:F1} ms" +
            (result.FailCount > 0 ? $" | Top error: {result.TopFailStatusCode}" : string.Empty));
    }

    private static void PrintSummary(
        StressTestOptions options,
        int? lastSustainableRate,
        LoadStepResult? breakpoint,
        IReadOnlyList<LoadStepResult> stepResults)
    {
        Console.WriteLine();
        Console.WriteLine("=== Stress Summary ===");

        if (lastSustainableRate.HasValue)
        {
            Console.WriteLine($"Last sustainable RPS: {lastSustainableRate.Value}");
        }
        else
        {
            Console.WriteLine("Last sustainable RPS: none (service failed at the first step)");
        }

        if (breakpoint is not null)
        {
            Console.WriteLine($"Breakpoint RPS: {breakpoint.TargetRate}");
            Console.WriteLine(
                $"Breakpoint reason: {DescribeBreakpointReason(breakpoint, options)}");
        }
        else
        {
            Console.WriteLine($"Breakpoint RPS: not reached (stable up to {options.MaxRate} RPS)");
        }

        var peakOkRps = stepResults.Max(step => step.OkRps);
        Console.WriteLine($"Peak achieved OK RPS: {peakOkRps:F1}");
    }

    private static string DescribeBreakpointReason(LoadStepResult result, StressTestOptions options)
    {
        if (result.TotalCount == 0)
        {
            return "no responses received";
        }

        if (result.FailPercent >= options.FailureThresholdPercent)
        {
            var reason = $"fail rate {result.FailPercent:F1}% (threshold {options.FailureThresholdPercent:F1}%)";

            if (result.TopFailStatusCode is not null)
            {
                reason += $", top status {result.TopFailStatusCode}";

                if (string.Equals(result.TopFailStatusCode, "429", StringComparison.Ordinal))
                {
                    reason += " (rate limit — increase Security:GlobalPermitLimit, disable Security:RateLimitingEnabled in Development, or raise --step-pause)";
                }
            }

            return reason;
        }

        return $"mean latency {result.MeanLatencyMs:F1}ms (threshold {options.MaxMeanLatencyMs}ms)";
    }

    private static async Task WaitBetweenStepsAsync(int pauseSeconds)
    {
        Console.WriteLine();
        Console.WriteLine($"Waiting {pauseSeconds}s before next step (rate-limit window reset)...");

        for (var remaining = pauseSeconds; remaining > 0; remaining--)
        {
            Console.Write($"\r  {remaining}s remaining   ");
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Console.WriteLine("\r  continuing...          ");
        Console.WriteLine();
    }
}
