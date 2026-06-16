namespace Aspire.CashFlow.ServiceDefaults.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; }

    public int RefreshTokenExpirationDays { get; init; }

    public int ClockSkewSeconds { get; init; }

    /// <summary>
    /// Optional Secrets Manager logical name (resolved via RuntimeSecrets / ISecretProvider).
    /// </summary>
    public string? SigningKeySecretName { get; init; }
}
