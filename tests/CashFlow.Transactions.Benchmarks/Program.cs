using BenchmarkDotNet.Running;
using CashFlow.Transactions.Benchmarks.Http;

namespace CashFlow.Transactions.Benchmarks;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "load", StringComparison.OrdinalIgnoreCase))
        {
            await TransactionHttpLoadRunner.RunAsync(args[1..]);
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "stress", StringComparison.OrdinalIgnoreCase))
        {
            await TransactionHttpStressRunner.RunAsync(args[1..]);
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
