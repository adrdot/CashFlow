namespace CashFlow.Auth.Application.Contracts;

public sealed class OAuthTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string? RefreshToken { get; init; }

    public string? IdToken { get; init; }

    public int ExpiresIn { get; init; }

    public string TokenType { get; init; } = "Bearer";

    public SessionState? Session { get; init; }

    public static OAuthTokenResponse FromLoginResult(LoginResult result)
    {
        return new OAuthTokenResponse
        {
            AccessToken = result.Token ?? string.Empty,
            RefreshToken = result.RefreshToken,
            ExpiresIn = result.Session is null
                ? 0
                : Math.Max(0, (int)(result.Session.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds),
            Session = result.Session
        };
    }
}
