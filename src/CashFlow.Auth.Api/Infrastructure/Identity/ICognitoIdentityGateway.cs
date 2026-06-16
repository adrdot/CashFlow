namespace CashFlow.Auth.Infrastructure.Identity;

public interface ICognitoIdentityGateway
{
    Task<CognitoAuthResult> AuthenticateAsync(string clientId, string username, string password, CancellationToken cancellationToken = default);

    Task<CognitoAuthResult> RespondToMfaChallengeAsync(string clientId, string challengeSession, string username, string mfaCode, string challengeName, CancellationToken cancellationToken = default);

    Task<CognitoAuthResult> RefreshTokenAsync(string clientId, string refreshToken, CancellationToken cancellationToken = default);

    Task<CognitoUserProfile?> GetUserAsync(string accessToken, CancellationToken cancellationToken = default);

    Task GlobalSignOutAsync(string accessToken, CancellationToken cancellationToken = default);
}