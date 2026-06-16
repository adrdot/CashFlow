namespace CashFlow.Auth.Application.Contracts;

public sealed class SessionState
{
    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string AuthenticationSource { get; init; } = string.Empty;

    public bool MfaRequired { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }
}
