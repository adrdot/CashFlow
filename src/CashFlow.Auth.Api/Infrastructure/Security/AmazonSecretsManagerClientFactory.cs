using Amazon;
using Amazon.SecretsManager;
using Aspire.CashFlow.ServiceDefaults.Aws;
using CashFlow.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.Infrastructure.Security;

internal static class AmazonSecretsManagerClientFactory
{
    public static IAmazonSecretsManager Create(SecretsManagerOptions options, AwsOptions awsOptions)
    {
        var regionName = AwsCredentialResolver.ResolveRegion(awsOptions, options.Region);

        if (options.UseCustomEndpoint)
        {
            var config = new AmazonSecretsManagerConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = regionName,
                UseHttp = options.ServiceUrl!.StartsWith(
                    "http://",
                    StringComparison.OrdinalIgnoreCase
                ),
            };

            return new AmazonSecretsManagerClient(
                AwsCredentialResolver.Resolve(awsOptions),
                config
            );
        }

        return new AmazonSecretsManagerClient(
            AwsCredentialResolver.Resolve(awsOptions),
            RegionEndpoint.GetBySystemName(regionName)
        );
    }

    public static SecretsManagerOptions ResolveOptions(IConfiguration configuration)
    {
        return configuration
                .GetSection(SecretsManagerOptions.SectionName)
                .Get<SecretsManagerOptions>() ?? new SecretsManagerOptions();
    }
}
