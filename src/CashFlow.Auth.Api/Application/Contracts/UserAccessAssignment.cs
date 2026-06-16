namespace CashFlow.Auth.Application.Contracts;

public sealed class UserAccessAssignment
{
    public string Email { get; init; } = string.Empty;

    public string GroupName { get; init; } = string.Empty;

    public string ApplicationRole { get; init; } = string.Empty;

    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}
