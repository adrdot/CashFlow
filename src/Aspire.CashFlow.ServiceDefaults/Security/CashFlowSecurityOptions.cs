namespace Aspire.CashFlow.ServiceDefaults.Security;

public sealed class CashFlowSecurityOptions
{
    public const string SectionName = "Security";

    public string[] AllowedOrigins { get; init; } = [];

    public bool RateLimitingEnabled { get; init; }

    public int GlobalPermitLimit { get; init; }

    public int GlobalWindowSeconds { get; init; }

    public int AuthLoginPermitLimit { get; init; }

    public int AuthLoginWindowSeconds { get; init; }
}
