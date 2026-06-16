namespace CashFlow.Auth.Infrastructure.Configuration;

public sealed class LocalAuthOptions
{
    public const string SectionName = "LocalAuth";

    public string MfaCode { get; init; } = "123456";

    public int MfaChallengeTtlMinutes { get; init; } = 5;
}
