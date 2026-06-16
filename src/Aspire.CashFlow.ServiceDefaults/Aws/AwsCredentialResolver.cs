using Amazon.Runtime;

namespace Aspire.CashFlow.ServiceDefaults.Aws;

public static class AwsCredentialResolver
{
    public static AWSCredentials Resolve(AwsOptions options)
    {
        var accessKey = FirstNonEmpty(
            options.AccessKey,
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")
        );
        var secretKey = FirstNonEmpty(
            options.SecretKey,
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")
        );

        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
        {
            return new BasicAWSCredentials(accessKey, secretKey);
        }

        return new EnvironmentVariablesAWSCredentials();
    }

    public static string ResolveRegion(AwsOptions awsOptions, string? serviceRegion = null)
    {
        var region = FirstNonEmpty(
            serviceRegion,
            awsOptions.Region,
            Environment.GetEnvironmentVariable("AWS_REGION"),
            Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
        );

        if (string.IsNullOrWhiteSpace(region))
        {
            throw new InvalidOperationException(
                "AWS region is not configured. Set AWS:Region or AWS_REGION."
            );
        }

        return region;
    }

    public static string ResolveLocalStackAccountId(AwsOptions options) =>
        FirstNonEmpty(options.LocalStackAccountId)
        ?? throw new InvalidOperationException(
            "AWS:LocalStackAccountId is required when normalizing LocalStack SQS URLs."
        );

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
