namespace CashFlow.Auth.Domain.Entities;

public sealed class IdentityProfile
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string AuthenticationSource { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    public bool MfaEnabled { get; init; }

    public string? ExternalDirectoryId { get; init; }

    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
}
