using Aspire.CashFlow.ServiceDefaults.Aws;

namespace CashFlow.Reporting.Infrastructure.Messaging;

internal static class LocalStackSqsUrlNormalizer
{
    public static string Normalize(string queueUrl, string serviceUrl, AwsOptions awsOptions)
    {
        if (string.IsNullOrWhiteSpace(queueUrl) || string.IsNullOrWhiteSpace(serviceUrl))
        {
            return queueUrl;
        }

        var queueName = queueUrl.TrimEnd('/').Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(queueName))
        {
            return queueUrl;
        }

        var accountId = AwsCredentialResolver.ResolveLocalStackAccountId(awsOptions);
        return $"{serviceUrl.TrimEnd('/')}/{accountId}/{queueName}";
    }
}
