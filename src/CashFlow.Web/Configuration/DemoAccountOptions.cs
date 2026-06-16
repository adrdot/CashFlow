namespace CashFlow.Web.Configuration;

public sealed class DemoAccountOptions
{
    public const string SectionName = "DemoAccount";

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string MfaCode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
