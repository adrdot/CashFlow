namespace CashFlow.Auth.Infrastructure.Configuration;

public sealed class LocalAuthOptions
{
    public const string SectionName = "LocalAuth";

    public string MfaCode { get; init; } = string.Empty;

    public int MfaChallengeTtlMinutes { get; init; }

    public string SeedAdminEmail { get; init; } = string.Empty;

    public string SeedAdminPassword { get; init; } = string.Empty;

    public string SeedAdminUserId { get; init; } = string.Empty;

    public string SeedAdminDisplayName { get; init; } = string.Empty;

    public string DefaultUserPassword { get; init; } = string.Empty;
}
