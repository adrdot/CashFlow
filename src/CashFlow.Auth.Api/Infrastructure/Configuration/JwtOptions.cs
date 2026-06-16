namespace CashFlow.Auth.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "CashFlow.Auth.Api";

    public string Audience { get; init; } = "CashFlow.Web";

    public string SigningKey { get; init; } = "dev-only-signing-key-change-me-1234567890";

    public int ExpirationMinutes { get; init; } = 60;

    public int RefreshTokenExpirationDays { get; init; } = 7;

    /// <summary>
    /// Optional Secrets Manager logical name (resolved via RuntimeSecrets / ISecretProvider).
    /// </summary>
    public string? SigningKeySecretName { get; init; }
}