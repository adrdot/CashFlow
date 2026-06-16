using Aspire.CashFlow.ServiceDefaults.Aws;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.Infrastructure;

public static class RuntimeSecretConfigurationExtensions
{
    public static WebApplicationBuilder AddCashFlowRuntimeSecrets(
        this WebApplicationBuilder builder
    )
    {
        var secretsManagerOptions = AmazonSecretsManagerClientFactory.ResolveOptions(
            builder.Configuration
        );
        var awsOptions =
            builder.Configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>()
            ?? new AwsOptions();
        var overrides = RuntimeSecretLoader.LoadConfigurationOverrides(
            builder.Configuration,
            secretsManagerOptions,
            awsOptions
        );

        if (overrides.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(overrides);
        }

        return builder;
    }
}
