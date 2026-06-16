namespace CashFlow.Reporting.Infrastructure.Messaging;

internal static class LocalStackSqsUrlNormalizer
{
    public static string Normalize(string queueUrl, string serviceUrl)
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

        return $"{serviceUrl.TrimEnd('/')}/000000000000/{queueName}";
    }
}
