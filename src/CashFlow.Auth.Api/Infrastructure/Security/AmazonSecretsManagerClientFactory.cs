using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using CashFlow.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.Infrastructure.Security;

internal static class AmazonSecretsManagerClientFactory
{
    public static IAmazonSecretsManager Create(SecretsManagerOptions options)
    {
        var regionName = string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region;

        if (options.UseCustomEndpoint)
        {
            var config = new AmazonSecretsManagerConfig
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = regionName,
                UseHttp = options.ServiceUrl!.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            };

            return new AmazonSecretsManagerClient(new BasicAWSCredentials("test", "test"), config);
        }

        return new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(regionName));
    }

    public static SecretsManagerOptions ResolveOptions(IConfiguration configuration)
    {
        return configuration.GetSection(SecretsManagerOptions.SectionName).Get<SecretsManagerOptions>()
            ?? new SecretsManagerOptions();
    }
}
