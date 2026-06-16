namespace CashFlow.Auth.Domain.Entities;

public sealed class JwtSession
{
    public string Token { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public UserAccount User { get; init; } = new();
}
