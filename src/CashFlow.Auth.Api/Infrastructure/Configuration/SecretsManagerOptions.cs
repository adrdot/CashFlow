namespace CashFlow.Auth.Infrastructure.Configuration;

public sealed class SecretsManagerOptions
{
    public const string SectionName = "SecretsManager";

    public string Region { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;

    public bool EnableCaching { get; init; }

    public int CacheDurationMinutes { get; init; }

    /// <summary>
    /// When set (e.g. LocalStack), the AWS SDK uses this endpoint.
    /// </summary>
    public string? ServiceUrl { get; init; }

    /// <summary>
    /// When true, configuration values under <c>Secrets:</c> are preferred over AWS Secrets Manager.
    /// </summary>
    public bool PreferConfiguration { get; init; }

    public bool UseCustomEndpoint => !string.IsNullOrWhiteSpace(ServiceUrl);
}
