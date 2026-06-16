namespace CashFlow.Auth.Application.Contracts;

public sealed class OAuthAuthorizationResult
{
    public bool Succeeded { get; init; }

    public string? RedirectUrl { get; init; }

    public LoginResult? LoginResult { get; init; }

    public static OAuthAuthorizationResult Redirect(string redirectUrl) =>
        new() { Succeeded = true, RedirectUrl = redirectUrl };

    public static OAuthAuthorizationResult Pending(LoginResult loginResult) =>
        new() { Succeeded = false, LoginResult = loginResult };
}
