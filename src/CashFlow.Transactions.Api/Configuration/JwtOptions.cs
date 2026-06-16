namespace CashFlow.Transactions.Api.Configuration;

public sealed record JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "CashFlow.Auth.Api";

    public string Audience { get; init; } = "CashFlow.Clients";

    public string SigningKey { get; init; } = "dev-signing-key-change-me-0123456789";
}