using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;

namespace CashFlow.Auth.Infrastructure.Identity;

public sealed class AwsCognitoIdentityGateway(IAmazonCognitoIdentityProvider cognitoClient) : ICognitoIdentityGateway
{
    public async Task<CognitoAuthResult> AuthenticateAsync(string clientId, string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await cognitoClient.InitiateAuthAsync(new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                ClientId = clientId,
                AuthParameters = new Dictionary<string, string>
                {
                    ["USERNAME"] = username,
                    ["PASSWORD"] = password
                }
            }, cancellationToken);

            return Map(response.AuthenticationResult, response.ChallengeName?.Value, response.Session);
        }
        catch (Exception ex) when (IsInvalidCredentialsException(ex))
        {
            return new CognitoAuthResult { IsInvalidCredentials = true };
        }
    }

    public async Task<CognitoAuthResult> RespondToMfaChallengeAsync(string clientId, string challengeSession, string username, string mfaCode, string challengeName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await cognitoClient.RespondToAuthChallengeAsync(new RespondToAuthChallengeRequest
            {
                ClientId = clientId,
                Session = challengeSession,
                ChallengeName = ParseChallengeName(challengeName),
                ChallengeResponses = new Dictionary<string, string>
                {
                    ["USERNAME"] = username,
                    [GetChallengeResponseKey(challengeName)] = mfaCode
                }
            }, cancellationToken);

            return Map(response.AuthenticationResult, response.ChallengeName?.Value, response.Session);
        }
        catch (CodeMismatchException)
        {
            return new CognitoAuthResult { IsInvalidMfaCode = true };
        }
        catch (ExpiredCodeException)
        {
            return new CognitoAuthResult { IsInvalidMfaCode = true };
        }
        catch (Exception ex) when (IsInvalidCredentialsException(ex))
        {
            return new CognitoAuthResult { IsInvalidCredentials = true };
        }
    }

    public async Task<CognitoAuthResult> RefreshTokenAsync(string clientId, string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await cognitoClient.InitiateAuthAsync(new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                ClientId = clientId,
                AuthParameters = new Dictionary<string, string>
                {
                    ["REFRESH_TOKEN"] = refreshToken
                }
            }, cancellationToken);

            return Map(response.AuthenticationResult, response.ChallengeName?.Value, response.Session);
        }
        catch (NotAuthorizedException)
        {
            return new CognitoAuthResult { IsInvalidRefreshToken = true };
        }
    }

    public async Task<CognitoUserProfile?> GetUserAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var response = await cognitoClient.GetUserAsync(new GetUserRequest
        {
            AccessToken = accessToken
        }, cancellationToken);

        var attributes = response.UserAttributes?.ToDictionary(attribute => attribute.Name, attribute => attribute.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        attributes.TryGetValue("email", out var email);
        attributes.TryGetValue("name", out var displayName);

        return new CognitoUserProfile
        {
            Username = response.Username,
            Email = email ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email ?? response.Username : displayName
        };
    }

    public async Task GlobalSignOutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            await cognitoClient.GlobalSignOutAsync(new GlobalSignOutRequest
            {
                AccessToken = accessToken
            }, cancellationToken);
        }
        catch (NotAuthorizedException)
        {
            // Token already invalid or expired — local logout can proceed.
        }
        catch (AmazonServiceException ex) when (IsUnsupportedCognitoFeature(ex))
        {
            // Cognito Local and other emulators may not implement GlobalSignOut.
        }
    }

    private static bool IsUnsupportedCognitoFeature(AmazonServiceException exception)
    {
        return exception.Message.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInvalidCredentialsException(Exception exception)
    {
        return exception is NotAuthorizedException
            or UserNotFoundException
            or InvalidPasswordException
            or UserNotConfirmedException;
    }

    private static CognitoAuthResult Map(AuthenticationResultType? authenticationResult, string? challengeName, string? challengeSession)
    {
        return new CognitoAuthResult
        {
            AccessToken = authenticationResult?.AccessToken,
            IdToken = authenticationResult?.IdToken,
            RefreshToken = authenticationResult?.RefreshToken,
            ExpiresIn = authenticationResult?.ExpiresIn ?? 0,
            ChallengeName = challengeName,
            ChallengeSession = challengeSession
        };
    }

    private static string GetChallengeResponseKey(string challengeName)
    {
        return challengeName switch
        {
            "SOFTWARE_TOKEN_MFA" => "SOFTWARE_TOKEN_MFA_CODE",
            _ => "SMS_MFA_CODE"
        };
    }

    private static ChallengeNameType ParseChallengeName(string challengeName)
    {
        return challengeName switch
        {
            "SOFTWARE_TOKEN_MFA" => ChallengeNameType.SOFTWARE_TOKEN_MFA,
            "SMS_MFA" => ChallengeNameType.SMS_MFA,
            _ => ChallengeNameType.SMS_MFA
        };
    }
}