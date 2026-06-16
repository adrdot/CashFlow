using Amazon;
using Amazon.KeyManagementService;
using Aspire.CashFlow.ServiceDefaults.Aws;
using CashFlow.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.Infrastructure.Security;

internal static class AmazonKmsClientFactory
{
    public static IAmazonKeyManagementService Create(KmsOptions options, AwsOptions awsOptions)
    {
        var regionName = AwsCredentialResolver.ResolveRegion(awsOptions, options.Region);

        if (options.UseCustomEndpoint)
        {
            var config = new AmazonKeyManagementServiceConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = regionName,
                UseHttp = options.ServiceUrl!.StartsWith(
                    "http://",
                    StringComparison.OrdinalIgnoreCase
                ),
            };

            return new AmazonKeyManagementServiceClient(
                AwsCredentialResolver.Resolve(awsOptions),
                config
            );
        }

        return new AmazonKeyManagementServiceClient(
            AwsCredentialResolver.Resolve(awsOptions),
            RegionEndpoint.GetBySystemName(regionName)
        );
    }

    public static KmsOptions ResolveOptions(IConfiguration configuration)
    {
        return configuration.GetSection(KmsOptions.SectionName).Get<KmsOptions>()
            ?? new KmsOptions();
    }
}
