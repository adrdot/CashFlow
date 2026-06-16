using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.ContractTests;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AuthWebApplicationFactory.IsolateFromLocalStackEnvironment();

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cognito:Enabled"] = "false",
                ["Cognito:UserPoolId"] = string.Empty,
                ["Cognito:ClientId"] = string.Empty,
                ["Cognito:ServiceUrl"] = string.Empty,
                ["Cognito:RequireMfa"] = "true",
                ["Cognito:OAuth:Enabled"] = "true",
                ["LocalAuth:MfaCode"] = "123456",
                ["CloudWatch:Enabled"] = "false",
                ["SecretsManager:PreferConfiguration"] = "true",
                ["SecretsManager:ServiceUrl"] = string.Empty,
                ["Kms:ServiceUrl"] = string.Empty,
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
                ["Jwt:Issuer"] = "CashFlow.Auth.Api",
                ["Jwt:Audience"] = "CashFlow.Web",
                ["Jwt:SigningKey"] = "dev-only-signing-key-change-me-1234567890",
                ["Secrets:Auth/JwtSigningKey"] = "dev-only-signing-key-change-me-1234567890"
            });
        });
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
