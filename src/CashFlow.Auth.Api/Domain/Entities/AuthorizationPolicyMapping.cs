namespace CashFlow.Auth.Domain.Entities;

public sealed class AuthorizationPolicyMapping
{
    public string GroupName { get; init; } = string.Empty;

    public string ApplicationRole { get; init; } = string.Empty;

    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}