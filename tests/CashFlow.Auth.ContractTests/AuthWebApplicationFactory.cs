using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.ContractTests;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AuthWebApplicationFactory.IsolateFromLocalStackEnvironment();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.Sources.Clear();
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Observability:PrometheusEnabled"] = "false",
                        ["Cognito:Enabled"] = "false",
                        ["Cognito:UserPoolId"] = string.Empty,
                        ["Cognito:ClientId"] = string.Empty,
                        ["Cognito:ServiceUrl"] = string.Empty,
                        ["Cognito:RequireMfa"] = "true",
                        ["Cognito:OAuth:Enabled"] = "true",
                        ["LocalAuth:MfaCode"] = "123456",
                        ["LocalAuth:MfaChallengeTtlMinutes"] = "5",
                        ["LocalAuth:SeedAdminEmail"] = "admin@cashflow.local",
                        ["LocalAuth:SeedAdminPassword"] = "Pass@word1",
                        ["LocalAuth:SeedAdminUserId"] = "b2f02e39-71d0-4d73-96df-f8626776f2a4",
                        ["LocalAuth:SeedAdminDisplayName"] = "Cash Flow Admin",
                        ["LocalAuth:DefaultUserPassword"] = "Pass@word1",
                        ["CloudWatch:Enabled"] = "false",
                        ["SecretsManager:PreferConfiguration"] = "true",
                        ["SecretsManager:ServiceUrl"] = string.Empty,
                        ["SecretsManager:Prefix"] = "cashflow/",
                        ["Kms:ServiceUrl"] = string.Empty,
                        ["AWS:Region"] = "us-east-1",
                        ["AWS:AccessKey"] = "test",
                        ["AWS:SecretKey"] = "test",
                        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
                        ["Jwt:Issuer"] = "CashFlow.Auth.Api",
                        ["Jwt:Audience"] = "CashFlow.Web",
                        ["Jwt:SigningKey"] = "dev-only-signing-key-change-me-1234567890",
                        ["Jwt:ExpirationMinutes"] = "60",
                        ["Jwt:RefreshTokenExpirationDays"] = "7",
                        ["Jwt:ClockSkewSeconds"] = "30",
                        ["Secrets:Auth/JwtSigningKey"] =
                            "dev-only-signing-key-change-me-1234567890",
                    }
                );
            }
        );
    }

    internal static void IsolateFromLocalStackEnvironment()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__reporting-db", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__Enabled", "false");
        Environment.SetEnvironmentVariable("Cognito__UserPoolId", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__ClientId", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__ServiceUrl", string.Empty);
        Environment.SetEnvironmentVariable("CloudWatch__Enabled", "false");
    }
}
