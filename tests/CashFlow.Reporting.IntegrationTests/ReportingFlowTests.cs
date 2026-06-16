using CashFlow.Reporting.Application;
using CashFlow.Reporting.Domain.Entities;
using CashFlow.Reporting.Infrastructure.Caching;
using CashFlow.Reporting.Infrastructure.Caching.Abstractions;
using CashFlow.Reporting.Infrastructure.Persistence;
using CashFlow.Reporting.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CashFlow.Reporting.IntegrationTests;

public sealed class DailySummaryProjectionTests
{
    [Fact]
    public async Task TransactionProjectionWriter_UpdatesDailySummaryIncrementally()
    {
        if (!await SqlTestDatabase.IsAvailableAsync())
        {
            return;
        }

        var connectionString = await SqlTestDatabase.EnsureReportingDatabaseAsync();
        var services = new ServiceCollection();
        services.AddDbContext<ReportingDbContext>(options =>
            options.UseSqlServer(connectionString)
        );
        services.AddSingleton<IReportCache, NullReportCache>();
        services.AddScoped<TransactionProjectionWriter>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await dbContext.Database.MigrateAsync();

        var writer = scope.ServiceProvider.GetRequiredService<TransactionProjectionWriter>();
        var userId = $"integration-{Guid.NewGuid():N}";
        var reportDate = new DateOnly(2026, 6, 15);

        await writer.ProjectAsync(
            Guid.NewGuid(),
            userId,
            "Credit",
            100m,
            "Sale",
            reportDate,
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );
        await writer.ProjectAsync(
            Guid.NewGuid(),
            userId,
            "Debit",
            40m,
            "Expense",
            reportDate,
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );

        var summary = await dbContext
            .DailySummaries.AsNoTracking()
            .SingleAsync(x => x.UserId == userId && x.ReportDate == reportDate);

        Assert.Equal(100m, summary.TotalCredits);
        Assert.Equal(40m, summary.TotalDebits);
        Assert.Equal(2, summary.TransactionVolume);
    }
}

public sealed class RedisReportCacheTests
{
    [Fact]
    public async Task RedisReportCache_StoresAndReturnsDailyReport()
    {
        if (!await RedisTestDatabase.IsAvailableAsync())
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddStackExchangeRedisCache(options =>
            options.Configuration = RedisTestDatabase.ConnectionString
        );
        services.Configure<ReportingCacheOptions>(_ => new ReportingCacheOptions());
        services.AddMetrics();
        services.AddSingleton<Infrastructure.Observability.ReportingMetrics>();
        services.AddSingleton<IReportCache, RedisReportCache>();

        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IReportCache>();
        var report = DailyReportBuilder.Build(
            new DateOnly(2026, 6, 12),
            new DailySummary
            {
                UserId = "cache-user",
                ReportDate = new DateOnly(2026, 6, 12),
                TotalCredits = 50m,
                TotalDebits = 10m,
                CreditEntryCount = 1,
                DebitEntryCount = 1,
                TransactionVolume = 2,
                LastUpdatedUtc = DateTimeOffset.UtcNow,
            }
        );

        await cache.SetAsync(
            "cache-user",
            new DateOnly(2026, 6, 12),
            report,
            CancellationToken.None
        );
        var cached = await cache.GetAsync(
            "cache-user",
            new DateOnly(2026, 6, 12),
            CancellationToken.None
        );

        Assert.NotNull(cached);
        Assert.Equal(50m, cached!.TotalCredits);
    }
}

internal static class SqlTestDatabase
{
    public static async Task<bool> IsAvailableAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__reporting-db")
            ?? "Server=127.0.0.1,1433;Database=reporting-db;User Id=sa;Password=CashFlow@Dev123!;TrustServerCertificate=True;Encrypt=False";

        try
        {
            var options = new DbContextOptionsBuilder<ReportingDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using var context = new ReportingDbContext(options);
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public static async Task<string> EnsureReportingDatabaseAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__reporting-db")
            ?? "Server=127.0.0.1,1433;Database=reporting-db;User Id=sa;Password=CashFlow@Dev123!;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new ReportingDbContext(options);
        await context.Database.MigrateAsync();
        return connectionString;
    }
}

internal static class RedisTestDatabase
{
    public const string ConnectionString = "localhost:6379";

    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var connection = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(
                ConnectionString
            );
            await connection.GetDatabase().PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
