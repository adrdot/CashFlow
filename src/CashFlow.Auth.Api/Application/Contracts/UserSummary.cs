namespace CashFlow.Auth.Application.Contracts;

public sealed class UserSummary
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool MfaEnabled { get; init; }

    public string AuthenticationSource { get; init; } = string.Empty;

    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
}
