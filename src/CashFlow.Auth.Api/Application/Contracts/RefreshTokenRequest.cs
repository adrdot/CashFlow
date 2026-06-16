namespace CashFlow.Auth.Application.Contracts;

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
