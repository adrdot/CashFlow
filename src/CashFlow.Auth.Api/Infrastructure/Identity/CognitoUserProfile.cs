namespace CashFlow.Auth.Infrastructure.Identity;

public sealed class CognitoUserProfile
{
    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}