using CashFlow.Reporting.Api.Observability;
using CashFlow.Reporting.Benchmarks.Http;
using CashFlow.Reporting.Benchmarks.Projection;
using CashFlow.Transactions.Benchmarks.Http;

namespace CashFlow.Reporting.Benchmarks;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "load", StringComparison.OrdinalIgnoreCase))
        {
            await ReportingHttpLoadRunner.RunAsync(args[1..]);
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "projection", StringComparison.OrdinalIgnoreCase))
        {
            await ProjectionStorageBenchmarkRunner.RunAsync(args[1..]);
            return;
        }

        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tests/CashFlow.Reporting.Benchmarks -- load [--url https://localhost:7090] [--rate 50] [--duration 30]");
        Console.WriteLine("  Environment: CASHFLOW_REPORTING_URL (Aspire reporting-api HTTPS URL)");
        Console.WriteLine("  dotnet run --project tests/CashFlow.Reporting.Benchmarks -- projection");
    }
}
