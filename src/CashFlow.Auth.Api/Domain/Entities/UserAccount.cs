namespace CashFlow.Auth.Domain.Entities;

public sealed class UserAccount
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}