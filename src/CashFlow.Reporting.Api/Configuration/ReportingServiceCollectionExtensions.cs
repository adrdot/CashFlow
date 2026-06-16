using Amazon.SQS;
using CashFlow.Reporting.Infrastructure.Caching;
using CashFlow.Reporting.Infrastructure.Exports;
using CashFlow.Reporting.Infrastructure.Messaging;
using CashFlow.Reporting.Infrastructure.Observability;
using CashFlow.Reporting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using StackExchange.Redis;

namespace CashFlow.Reporting.Api.Configuration;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReportingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<ReportingQueueStats>();
        services.AddSingleton<ReportingMetrics>();
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddMeter(ReportingMetrics.MeterName));

        services.Configure<ReportingCacheOptions>(configuration.GetSection(ReportingCacheOptions.SectionName));
        services.Configure<ReportingRedisOptions>(configuration.GetSection(ReportingRedisOptions.SectionName));
        services.AddReportingCache(configuration);

        var connectionString = configuration.GetConnectionString("reporting-db");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ReportingDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<Application.Abstractions.IReportRepository, SqlReportRepository>();
        }
        else if (environment.IsDevelopment())
        {
            services.AddSingleton<Application.Abstractions.IReportRepository, InMemoryReportRepository>();
        }
        else
        {
            throw new InvalidOperationException("Connection string 'reporting-db' is required outside development.");
        }

        services.AddScoped<Application.Abstractions.IReportExportService, ReportExportService>();

        return services;
    }

    public static IServiceCollection AddReportingProjectionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ReportingMessagingOptions>(configuration.GetSection(ReportingMessagingOptions.SectionName));
        services.Configure<ReportingCacheOptions>(configuration.GetSection(ReportingCacheOptions.SectionName));
        services.Configure<ReportingRedisOptions>(configuration.GetSection(ReportingRedisOptions.SectionName));
        services.AddReportingCache(configuration);

        services.AddSingleton<ReportingQueueStats>();
        services.AddSingleton<ReportingMetrics>();
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddMeter(ReportingMetrics.MeterName));

        var connectionString = configuration.GetConnectionString("reporting-db");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'reporting-db' is required for the reporting projection worker.");
        }

        services.AddDbContext<ReportingDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<Application.Abstractions.IReportRepository, SqlReportRepository>();
        services.AddScoped<TransactionProjectionWriter>();

        var messagingOptions = configuration.GetSection(ReportingMessagingOptions.SectionName).Get<ReportingMessagingOptions>()
            ?? new ReportingMessagingOptions();
        if (string.IsNullOrWhiteSpace(messagingOptions.SqsQueueUrl))
        {
            throw new InvalidOperationException("Messaging:SqsQueueUrl is required for the reporting projection worker.");
        }

        services.AddSingleton<IAmazonSQS>(_ => SqsClientFactory.Create(messagingOptions));
        services.AddHostedService<SqsTransactionProjectionConsumer>();
        services.AddHostedService<ReportingSqsQueueMonitorBackgroundService>();

        return services;
    }

    private static IServiceCollection AddReportingCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(ReportingRedisOptions.SectionName).Get<ReportingRedisOptions>()
            ?? new ReportingRedisOptions();

        if (redisOptions.Enabled && !string.IsNullOrWhiteSpace(redisOptions.Configuration))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisOptions.Configuration;
            });
            services.AddSingleton<Application.Abstractions.IReportCache, RedisReportCache>();
            services.AddHealthChecks()
                .AddCheck<RedisReportCacheHealthCheck>(
                    "redis-report-cache",
                    failureStatus: HealthStatus.Degraded,
                    tags: ["ready"]);
        }
        else
        {
            services.AddSingleton<Application.Abstractions.IReportCache, NullReportCache>();
        }

        return services;
    }

    public static async Task ApplyReportingMigrationsAsync(this WebApplication app)
    {
        var connectionString = app.Configuration.GetConnectionString("reporting-db");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}

internal sealed class RedisReportCacheHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var redisOptions = configuration.GetSection(ReportingRedisOptions.SectionName).Get<ReportingRedisOptions>()
            ?? new ReportingRedisOptions();

        if (!redisOptions.Enabled || string.IsNullOrWhiteSpace(redisOptions.Configuration))
        {
            return HealthCheckResult.Healthy("Redis cache is disabled.");
        }

        try
        {
            var connection = await ConnectionMultiplexer.ConnectAsync(redisOptions.Configuration);
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis report cache is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Redis report cache is unavailable; reads fall back to SQL.", ex);
        }
    }
}
