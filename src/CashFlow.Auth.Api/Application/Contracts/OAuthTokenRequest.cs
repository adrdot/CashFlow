namespace CashFlow.Auth.Application.Contracts;

public sealed class OAuthTokenRequest
{
    public string GrantType { get; init; } = "authorization_code";

    public string Code { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;

    public string? ClientId { get; init; }
}
