using Amazon.SQS;
using Aspire.CashFlow.ServiceDefaults.Aws;
using Aspire.CashFlow.ServiceDefaults.Observability;
using CashFlow.Reporting.Infrastructure.Caching;
using CashFlow.Reporting.Infrastructure.Caching.Abstractions;
using CashFlow.Reporting.Infrastructure.Exports;
using CashFlow.Reporting.Infrastructure.Exports.Abstractions;
using CashFlow.Reporting.Infrastructure.Messaging;
using CashFlow.Reporting.Infrastructure.Observability;
using CashFlow.Reporting.Infrastructure.Persistence;
using CashFlow.Reporting.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CashFlow.Reporting.Api.Configuration;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReportingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.AddCashFlowMeter<ReportingMetrics>(ReportingMetrics.MeterName);

        services.Configure<ReportingCacheOptions>(
            configuration.GetSection(ReportingCacheOptions.SectionName)
        );
        services.Configure<ReportingRedisOptions>(
            configuration.GetSection(ReportingRedisOptions.SectionName)
        );
        services.AddReportingCache(configuration);

        var connectionString = configuration.GetConnectionString("reporting-db");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ReportingDbContext>(options =>
                options.UseSqlServer(connectionString)
            );
            services.AddScoped<IReportRepository, SqlReportRepository>();
        }
        else if (environment.IsDevelopment())
        {
            services.AddSingleton<IReportRepository, InMemoryReportRepository>();
        }
        else
        {
            throw new InvalidOperationException(
                "Connection string 'reporting-db' is required outside development."
            );
        }

        services.AddScoped<ICsvReportExporter, CsvReportExportService>();
        services.AddScoped<IPdfReportExporter, PdfReportExportService>();

        return services;
    }

    public static IServiceCollection AddReportingProjectionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<ReportingMessagingOptions>(
            configuration.GetSection(ReportingMessagingOptions.SectionName)
        );
        services.Configure<ReportingCacheOptions>(
            configuration.GetSection(ReportingCacheOptions.SectionName)
        );
        services.Configure<ReportingRedisOptions>(
            configuration.GetSection(ReportingRedisOptions.SectionName)
        );
        services.AddReportingCache(configuration);

        services.AddCashFlowMeter<ReportingMetrics>(ReportingMetrics.MeterName);

        var connectionString = configuration.GetConnectionString("reporting-db");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'reporting-db' is required for the reporting projection worker."
            );
        }

        services.AddDbContext<ReportingDbContext>(options =>
            options.UseSqlServer(connectionString)
        );
        services.AddScoped<IReportRepository, SqlReportRepository>();
        services.AddScoped<TransactionProjectionWriter>();

        var messagingOptions =
            configuration
                .GetSection(ReportingMessagingOptions.SectionName)
                .Get<ReportingMessagingOptions>() ?? new ReportingMessagingOptions();
        if (string.IsNullOrWhiteSpace(messagingOptions.SqsQueueUrl))
        {
            throw new InvalidOperationException(
                "Messaging:SqsQueueUrl is required for the reporting projection worker."
            );
        }

        services.AddSingleton<IAmazonSQS>(sp =>
            SqsClientFactory.Create(
                messagingOptions,
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AwsOptions>>().Value
            )
        );
        services.AddHostedService<SqsTransactionProjectionConsumer>();

        return services;
    }

    private static IServiceCollection AddReportingCache(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var redisOptions =
            configuration.GetSection(ReportingRedisOptions.SectionName).Get<ReportingRedisOptions>()
            ?? new ReportingRedisOptions();

        if (redisOptions.Enabled && !string.IsNullOrWhiteSpace(redisOptions.Configuration))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisOptions.Configuration;
            });
            services.AddSingleton<IReportCache, RedisReportCache>();
            services
                .AddHealthChecks()
                .AddCheck<RedisReportCacheHealthCheck>(
                    "redis-report-cache",
                    failureStatus: HealthStatus.Degraded,
                    tags: ["ready"]
                );
        }
        else
        {
            services.AddSingleton<IReportCache, NullReportCache>();
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

internal sealed class RedisReportCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await cache.GetAsync("cashflow:reporting:health", cancellationToken);
            return HealthCheckResult.Healthy("Redis report cache is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(
                "Redis report cache is unavailable; reads fall back to SQL.",
                ex
            );
        }
    }
}
