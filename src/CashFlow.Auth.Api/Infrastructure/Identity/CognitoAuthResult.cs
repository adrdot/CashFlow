namespace CashFlow.Auth.Infrastructure.Identity;

public sealed class CognitoAuthResult
{
    public string? AccessToken { get; init; }

    public string? IdToken { get; init; }

    public string? RefreshToken { get; init; }

    public int ExpiresIn { get; init; }

    public string? ChallengeName { get; init; }

    public string? ChallengeSession { get; init; }

    public bool RequiresChallenge =>
        !string.IsNullOrWhiteSpace(ChallengeName) && !string.IsNullOrWhiteSpace(ChallengeSession);

    public bool IsInvalidCredentials { get; init; }

    public bool IsInvalidMfaCode { get; init; }

    public bool IsInvalidRefreshToken { get; init; }
}
