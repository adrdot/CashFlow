namespace CashFlow.Auth.Infrastructure.Configuration;

public sealed class KmsOptions
{
    public const string SectionName = "Kms";

    public string Region { get; init; } = string.Empty;

    public string DefaultKeyId { get; init; } = string.Empty;

    public string SecretsKeyId { get; init; } = string.Empty;

    public string StorageKeyId { get; init; } = string.Empty;

    /// <summary>
    /// When set (e.g. LocalStack), the AWS SDK uses this endpoint.
    /// </summary>
    public string? ServiceUrl { get; init; }

    public bool UseCustomEndpoint => !string.IsNullOrWhiteSpace(ServiceUrl);
}
