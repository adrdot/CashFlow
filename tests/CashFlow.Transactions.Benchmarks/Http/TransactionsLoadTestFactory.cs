using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Transactions.Benchmarks.Http;

internal sealed class TransactionsLoadTestFactory : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(ResolveApiContentRoot());
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.Sources.Clear();
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:transactions-db"] = string.Empty,
                        ["EventStore:HttpEndpoint"] = string.Empty,
                        ["Messaging:Enabled"] = "false",
                        ["Cognito:Enabled"] = "false",
                        ["Jwt:Issuer"] = LoadTestJwtHelper.DefaultIssuer,
                        ["Jwt:Audience"] = LoadTestJwtHelper.DefaultAudience,
                        ["Jwt:SigningKey"] = LoadTestJwtHelper.DefaultSigningKey,
                        ["Security:RateLimitingEnabled"] = "false",
                    }
                );
            }
        );
    }

    private static string ResolveApiContentRoot()
    {
        var benchmarkDir = Path.GetDirectoryName(
            typeof(TransactionsLoadTestFactory).Assembly.Location
        )!;
        var apiRoot = Path.GetFullPath(
            Path.Combine(
                benchmarkDir,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "CashFlow.Transactions.Api"
            )
        );

        if (!Directory.Exists(apiRoot))
        {
            throw new DirectoryNotFoundException($"API content root not found: {apiRoot}");
        }

        return apiRoot;
    }
}
