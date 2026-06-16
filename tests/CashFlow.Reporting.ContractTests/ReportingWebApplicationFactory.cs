using CashFlow.Reporting.Infrastructure.Caching;
using CashFlow.Reporting.Infrastructure.Caching.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CashFlow.Reporting.ContractTests;

internal sealed class ReportingWebApplicationFactory : WebApplicationFactory<Program>
{
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
                        ["Reporting:Redis:Configuration"] = string.Empty,
                        ["Security:RateLimitingEnabled"] = "false",
                        ["CloudWatch:Enabled"] = "false",
                        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
                        ["Jwt:Issuer"] = "CashFlow.Auth.Api",
                        ["Jwt:Audience"] = "CashFlow.Web",
                        ["Jwt:SigningKey"] = "dev-only-signing-key-change-me-1234567890",
                    }
                );
            }
        );

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReportCache>();
            services.AddSingleton<IReportCache, NullReportCache>();
        });
    }
}
