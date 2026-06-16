namespace CashFlow.Auth.Application.Contracts;

public sealed class LoginResult
{
    public bool Succeeded { get; init; }

    public bool RequiresMfa { get; init; }

    public string? ErrorMessage { get; init; }

    public string? Token { get; init; }

    public string? RefreshToken { get; init; }

    public string? ChallengeSession { get; init; }

    public string? ChallengeName { get; init; }

    public SessionState? Session { get; init; }

    public static LoginResult Success(string token, SessionState session, string? refreshToken = null)
    {
        return new LoginResult
        {
            Succeeded = true,
            Token = token,
            RefreshToken = refreshToken,
            Session = session
        };
    }

    public static LoginResult Failure(string message)
    {
        return new LoginResult
        {
            Succeeded = false,
            ErrorMessage = message
        };
    }

    public static LoginResult MfaChallenge(string challengeSession, string challengeName, string message)
    {
        return new LoginResult
        {
            Succeeded = false,
            RequiresMfa = true,
            ChallengeSession = challengeSession,
            ChallengeName = challengeName,
            ErrorMessage = message
        };
    }
}