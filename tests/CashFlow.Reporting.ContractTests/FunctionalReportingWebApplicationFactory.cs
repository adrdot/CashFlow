using CashFlow.Reporting.Infrastructure.Caching;
using CashFlow.Reporting.Infrastructure.Caching.Abstractions;
using CashFlow.Reporting.Infrastructure.Persistence;
using CashFlow.Reporting.Infrastructure.Persistence.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CashFlow.Reporting.ContractTests;

internal sealed class FunctionalReportingWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"functional-reporting-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ContractTestEnvironment.IsolateFromLocalStack();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.Sources.Clear();
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:reporting-db"] = string.Empty,
                        ["Messaging:SqsQueueUrl"] = string.Empty,
                        ["Cognito:Enabled"] = "false",
                        ["Cognito:UserPoolId"] = string.Empty,
                        ["Cognito:ClientId"] = string.Empty,
                        ["Cognito:ServiceUrl"] = string.Empty,
                        ["Reporting:Redis:Enabled"] = "false",
                        ["Security:RateLimitingEnabled"] = "false",
                        ["CloudWatch:Enabled"] = "false",
                        ["Jwt:Issuer"] = "CashFlow.Auth.Api",
                        ["Jwt:Audience"] = "CashFlow.Web",
                        ["Jwt:SigningKey"] = "dev-only-signing-key-change-me-1234567890",
                    }
                );
            }
        );

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReportRepository>();
            services.RemoveAll<IReportCache>();
            services.RemoveAll<DbContextOptions<ReportingDbContext>>();
            services.RemoveAll<ReportingDbContext>();

            services.AddDbContext<ReportingDbContext>(options =>
                options
                    .UseInMemoryDatabase(databaseName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            );
            services.AddScoped<IReportRepository, SqlReportRepository>();
            services.AddSingleton<IReportCache, NullReportCache>();
            services.AddScoped<TransactionProjectionWriter>();
        });
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
