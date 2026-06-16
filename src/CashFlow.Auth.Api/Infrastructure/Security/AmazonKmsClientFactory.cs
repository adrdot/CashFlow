using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using CashFlow.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.Infrastructure.Security;

internal static class AmazonKmsClientFactory
{
    public static IAmazonKeyManagementService Create(KmsOptions options)
    {
        var regionName = string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region;

        if (options.UseCustomEndpoint)
        {
            var config = new AmazonKeyManagementServiceConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = regionName,
                UseHttp = options.ServiceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            };

            return new AmazonKeyManagementServiceClient(new BasicAWSCredentials("test", "test"), config);
        }

        return new AmazonKeyManagementServiceClient(RegionEndpoint.GetBySystemName(regionName));
    }

    public static KmsOptions ResolveOptions(IConfiguration configuration)
    {
        return configuration.GetSection(KmsOptions.SectionName).Get<KmsOptions>()
            ?? new KmsOptions();
    }
}
