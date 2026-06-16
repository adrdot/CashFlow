namespace CashFlow.Web.Configuration;

public sealed class DemoAccountOptions
{
    public const string SectionName = "DemoAccount";

    public string Email { get; init; } = "admin@cashflow.local";

    public string Password { get; init; } = "Pass@word1";

    public string MfaCode { get; init; } = "123456";

    public string Description { get; init; } = string.Empty;
}
