namespace CashFlow.Auth.Infrastructure.Configuration;

public sealed class SecretsManagerOptions
{
    public const string SectionName = "SecretsManager";

    public string Region { get; init; } = "us-east-1";

    public string Prefix { get; init; } = "cashflow/";

    public bool EnableCaching { get; init; } = true;

    public int CacheDurationMinutes { get; init; } = 5;

    /// <summary>
    /// When set (e.g. LocalStack at http://localhost:4566), the AWS SDK uses this endpoint.
    /// </summary>
    public string? ServiceUrl { get; init; }

    /// <summary>
    /// When true, configuration values under <c>Secrets:</c> are preferred over AWS Secrets Manager.
    /// Recommended for local development without LocalStack.
    /// </summary>
    public bool PreferConfiguration { get; init; }

    public bool UseCustomEndpoint => !string.IsNullOrWhiteSpace(ServiceUrl);
}
