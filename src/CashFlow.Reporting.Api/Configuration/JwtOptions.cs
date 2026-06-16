namespace CashFlow.Reporting.Api.Configuration;

public sealed record JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "CashFlow.Auth.Api";

    public string Audience { get; init; } = "CashFlow.Web";

    public string SigningKey { get; init; } = "dev-only-signing-key-change-me-1234567890";
}