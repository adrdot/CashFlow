using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Aspire.CashFlow.ServiceDefaults.Aws;
using CashFlow.Transactions.Infrastructure.Messaging;

namespace CashFlow.Transactions.Infrastructure.Messaging;

public static class SnsClientFactory
{
    public static IAmazonSimpleNotificationService Create(
        MessagingOptions messagingOptions,
        AwsOptions awsOptions
    )
    {
        var region = AwsCredentialResolver.ResolveRegion(awsOptions, messagingOptions.Region);
        var config = new AmazonSimpleNotificationServiceConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
        };

        if (!string.IsNullOrWhiteSpace(messagingOptions.ServiceUrl))
        {
            config.ServiceURL = messagingOptions.ServiceUrl;
            config.UseHttp = messagingOptions.ServiceUrl.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase
            );
        }
        else if (!string.IsNullOrWhiteSpace(awsOptions.ServiceUrl))
        {
            config.ServiceURL = awsOptions.ServiceUrl;
            config.UseHttp = awsOptions.ServiceUrl.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase
            );
        }

        return new AmazonSimpleNotificationServiceClient(
            AwsCredentialResolver.Resolve(awsOptions),
            config
        );
    }
}
