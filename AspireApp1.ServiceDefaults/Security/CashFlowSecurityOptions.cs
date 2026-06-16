namespace AspireApp1.ServiceDefaults.Security;

public sealed class CashFlowSecurityOptions
{
    public const string SectionName = "Security";

    public string[] AllowedOrigins { get; init; } = [];

    public bool RateLimitingEnabled { get; init; } = true;

    public int GlobalPermitLimit { get; init; } = 200;

    public int GlobalWindowSeconds { get; init; } = 60;

    public int AuthLoginPermitLimit { get; init; } = 10;

    public int AuthLoginWindowSeconds { get; init; } = 60;
}
