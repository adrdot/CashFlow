using CashFlow.Reporting.Application.Abstractions;
using CashFlow.Reporting.Infrastructure.Caching;
using CashFlow.Reporting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Reporting.Benchmarks.Projection;

internal static class ProjectionStorageBenchmarkRunner
{
    private const int Iterations = 500;

    public static async Task RunAsync(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__reporting-db")
            ?? "Server=127.0.0.1,1433;Database=reporting-db;User Id=sa;Password=CashFlow@Dev123!;TrustServerCertificate=True;Encrypt=False";

        await using var sqlContext = CreateContext(connectionString);
        await sqlContext.Database.MigrateAsync();

        var sqlElapsed = await BenchmarkSqlProjectionAsync(sqlContext);
        var dynamoElapsed = DynamoDbProjectionIdempotencyStore.EstimateConditionalPutLatency(Iterations);

        Console.WriteLine();
        Console.WriteLine("=== Projection Idempotency Benchmark (spike) ===");
        Console.WriteLine($"SQL optimized insert + summary UPSERT ({Iterations} new keys): {sqlElapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine($"DynamoDB conditional PutItem estimate ({Iterations} ops, in-memory stub): {dynamoElapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine();
        Console.WriteLine("Orientação de decisão: ver docs/adr/002-infraestrutura-stack-recursos.md");
        Console.WriteLine("SQL remains primary storage when optimized path sustains relay throughput with lag ~0.");
    }

    private static async Task<TimeSpan> BenchmarkSqlProjectionAsync(ReportingDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReportCache, NullReportCache>();
        services.AddScoped<TransactionProjectionWriter>();
        services.AddScoped(_ => dbContext);

        await using var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<TransactionProjectionWriter>();

        var userId = $"bench-{Guid.NewGuid():N}";
        var reportDate = new DateOnly(2026, 6, 15);
        var started = DateTime.UtcNow;

        for (var index = 0; index < Iterations; index++)
        {
            await writer.ProjectAsync(
                Guid.NewGuid(),
                userId,
                index % 2 == 0 ? "Credit" : "Debit",
                10m + index,
                $"Benchmark {index}",
                reportDate,
                DateTimeOffset.UtcNow,
                CancellationToken.None);
        }

        return DateTime.UtcNow - started;
    }

    private static ReportingDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ReportingDbContext(options);
    }
}
