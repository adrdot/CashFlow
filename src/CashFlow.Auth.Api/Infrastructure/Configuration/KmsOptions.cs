namespace CashFlow.Auth.Infrastructure.Configuration;

public sealed class KmsOptions
{
    public const string SectionName = "Kms";

    public string Region { get; init; } = "us-east-1";

    public string DefaultKeyId { get; init; } = "alias/cashflow-default";

    public string SecretsKeyId { get; init; } = "alias/cashflow-secrets";

    public string StorageKeyId { get; init; } = "alias/cashflow-storage";

    /// <summary>
    /// When set (e.g. LocalStack at http://localhost:4566), the AWS SDK uses this endpoint.
    /// </summary>
    public string? ServiceUrl { get; init; }

    public bool UseCustomEndpoint => !string.IsNullOrWhiteSpace(ServiceUrl);
}
