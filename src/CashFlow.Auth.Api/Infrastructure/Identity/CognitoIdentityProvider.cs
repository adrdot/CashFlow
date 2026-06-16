using System.IdentityModel.Tokens.Jwt;
using Aspire.CashFlow.ServiceDefaults.Authentication;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Identity.Abstractions;
using CashFlow.Auth.Infrastructure.Persistence.Abstractions;
using CashFlow.Auth.Infrastructure.Security.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.Infrastructure.Identity;

public sealed class CognitoIdentityProvider(
    ICognitoIdentityGateway cognitoIdentityGateway,
    IUserAccountStore userAccountStore,
    IPasswordVerifier passwordVerifier,
    ITokenService tokenService,
    LocalMfaChallengeStore localMfaChallengeStore,
    IOptions<CognitoOptions> options,
    IOptions<LocalAuthOptions> localAuthOptions
) : IIdentityProvider
{
    private readonly CognitoOptions cognitoOptions = options.Value;
    private readonly LocalAuthOptions localAuth = localAuthOptions.Value;

    public async Task<LoginResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (cognitoOptions.IsConfigured)
        {
            return await AuthenticateWithCognitoAsync(request, cancellationToken);
        }

        return await AuthenticateWithLocalFallbackAsync(request, cancellationToken);
    }

    public async Task<SessionState?> ValidateSessionAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        if (cognitoOptions.IsConfigured)
        {
            var profile = await cognitoIdentityGateway.GetUserAsync(token, cancellationToken);
            return profile is null
                ? null
                : BuildSessionFromAccessToken(token, profile.Email, profile.DisplayName);
        }

        var session = tokenService.ValidateToken(token);
        return session is null ? null : Enrich(session);
    }

    public async Task RevokeSessionAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        if (cognitoOptions.IsConfigured && !cognitoOptions.UseLocalStack)
        {
            await cognitoIdentityGateway.GlobalSignOutAsync(token, cancellationToken);
        }
    }

    public async Task<LoginResult> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return LoginResult.Failure("Refresh token is required.");
        }

        if (cognitoOptions.IsConfigured)
        {
            return await RefreshWithCognitoAsync(refreshToken, cancellationToken);
        }

        return await RefreshWithLocalFallbackAsync(refreshToken, cancellationToken);
    }

    private async Task<LoginResult> RefreshWithCognitoAsync(
        string refreshToken,
        CancellationToken cancellationToken
    )
    {
        var authResult = await cognitoIdentityGateway.RefreshTokenAsync(
            cognitoOptions.ClientId,
            refreshToken,
            cancellationToken
        );

        if (authResult.IsInvalidRefreshToken)
        {
            return LoginResult.Failure("Refresh token is invalid or expired.");
        }

        if (string.IsNullOrWhiteSpace(authResult.AccessToken))
        {
            return LoginResult.Failure("Authentication service unavailable. Try again later.");
        }

        var session = BuildSessionFromTokens(authResult.AccessToken, authResult.IdToken);
        var rotatedRefreshToken = string.IsNullOrWhiteSpace(authResult.RefreshToken)
            ? refreshToken
            : authResult.RefreshToken;

        return LoginResult.Success(authResult.AccessToken, session, rotatedRefreshToken);
    }

    private async Task<LoginResult> RefreshWithLocalFallbackAsync(
        string refreshToken,
        CancellationToken cancellationToken
    )
    {
        var email = tokenService.ValidateRefreshToken(refreshToken);
        if (string.IsNullOrWhiteSpace(email))
        {
            return LoginResult.Failure("Refresh token is invalid or expired.");
        }

        var userAccount = await userAccountStore.FindByEmailAsync(email, cancellationToken);
        if (userAccount is null || !userAccount.IsActive)
        {
            return LoginResult.Failure("Refresh token is invalid or expired.");
        }

        var accessToken = tokenService.CreateToken(userAccount);
        var session = tokenService.ValidateToken(accessToken);
        if (session is null)
        {
            return LoginResult.Failure("Authentication service unavailable. Try again later.");
        }

        var rotatedRefreshToken = tokenService.CreateRefreshToken(userAccount);
        return LoginResult.Success(accessToken, Enrich(session), rotatedRefreshToken);
    }

    private async Task<LoginResult> AuthenticateWithCognitoAsync(
        LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        if (
            !string.IsNullOrWhiteSpace(request.ChallengeSession)
            && !string.IsNullOrWhiteSpace(request.MfaCode)
        )
        {
            if (localMfaChallengeStore.IsLocalChallenge(request.ChallengeSession))
            {
                if (
                    !localMfaChallengeStore.TryCompleteWithPendingCognitoAuth(
                        request.ChallengeSession,
                        request.Email,
                        request.MfaCode,
                        localAuth.MfaCode,
                        out var pendingAuth
                    )
                    || pendingAuth is null
                    || string.IsNullOrWhiteSpace(pendingAuth.AccessToken)
                )
                {
                    return LoginResult.Failure("Invalid MFA code.");
                }

                var pendingSession = BuildSessionFromTokens(
                    pendingAuth.AccessToken,
                    pendingAuth.IdToken
                );
                return LoginResult.Success(
                    pendingAuth.AccessToken,
                    pendingSession,
                    pendingAuth.RefreshToken
                );
            }

            var authResult = await cognitoIdentityGateway.RespondToMfaChallengeAsync(
                cognitoOptions.ClientId,
                request.ChallengeSession,
                request.Email,
                request.MfaCode,
                request.ChallengeName ?? "SMS_MFA",
                cancellationToken
            );

            return MapCognitoAuthResult(authResult);
        }

        CognitoAuthResult initialAuthResult;

        initialAuthResult = await cognitoIdentityGateway.AuthenticateAsync(
            cognitoOptions.ClientId,
            request.Email,
            request.Password,
            cancellationToken
        );

        if (ShouldApplyLocalMfaGate(initialAuthResult))
        {
            var challengeSession = localMfaChallengeStore.CreateChallenge(
                request.Email,
                TimeSpan.FromMinutes(localAuth.MfaChallengeTtlMinutes),
                initialAuthResult
            );

            return LoginResult.MfaChallenge(
                challengeSession,
                "LOCAL_MFA",
                $"Enter the local MFA code ({localAuth.MfaCode})."
            );
        }

        return MapCognitoAuthResult(initialAuthResult);
    }

    private bool ShouldApplyLocalMfaGate(CognitoAuthResult authResult)
    {
        return cognitoOptions.RequireMfa
            && cognitoOptions.UseLocalStack
            && !authResult.RequiresChallenge
            && !authResult.IsInvalidCredentials
            && !authResult.IsInvalidMfaCode
            && !string.IsNullOrWhiteSpace(authResult.AccessToken);
    }

    private LoginResult MapCognitoAuthResult(CognitoAuthResult authResult)
    {
        if (authResult.RequiresChallenge)
        {
            return LoginResult.MfaChallenge(
                authResult.ChallengeSession!,
                authResult.ChallengeName!,
                "Enter the MFA code generated for your Cognito user."
            );
        }

        if (authResult.IsInvalidCredentials)
        {
            return LoginResult.Failure("Invalid email or password.");
        }

        if (authResult.IsInvalidMfaCode)
        {
            return LoginResult.Failure("Invalid MFA code.");
        }

        if (string.IsNullOrWhiteSpace(authResult.AccessToken))
        {
            return LoginResult.Failure("Authentication service unavailable. Try again later.");
        }

        var session = BuildSessionFromTokens(authResult.AccessToken, authResult.IdToken);
        return LoginResult.Success(authResult.AccessToken, session, authResult.RefreshToken);
    }

    private async Task<LoginResult> AuthenticateWithLocalFallbackAsync(
        LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        if (
            cognitoOptions.RequireMfa
            && !string.IsNullOrWhiteSpace(request.ChallengeSession)
            && !string.IsNullOrWhiteSpace(request.MfaCode)
        )
        {
            if (
                !localMfaChallengeStore.Validate(
                    request.ChallengeSession,
                    request.Email,
                    request.MfaCode,
                    localAuth.MfaCode
                )
            )
            {
                return LoginResult.Failure("Invalid MFA code.");
            }

            return await CompleteLocalLoginAsync(request, cancellationToken);
        }

        var userAccount = await userAccountStore.FindByEmailAsync(request.Email, cancellationToken);
        if (userAccount is null || !userAccount.IsActive)
        {
            return LoginResult.Failure("Invalid email or password.");
        }

        if (!passwordVerifier.Verify(userAccount, request.Password))
        {
            return LoginResult.Failure("Invalid email or password.");
        }

        if (cognitoOptions.RequireMfa)
        {
            var challengeSession = localMfaChallengeStore.CreateChallenge(
                request.Email,
                TimeSpan.FromMinutes(localAuth.MfaChallengeTtlMinutes)
            );

            return LoginResult.MfaChallenge(
                challengeSession,
                "SOFTWARE_TOKEN_MFA",
                $"Enter the local MFA code ({localAuth.MfaCode})."
            );
        }

        return await CompleteLocalLoginAsync(request, cancellationToken);
    }

    private async Task<LoginResult> CompleteLocalLoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        var userAccount = await userAccountStore.FindByEmailAsync(request.Email, cancellationToken);
        if (userAccount is null || !userAccount.IsActive)
        {
            return LoginResult.Failure("Invalid email or password.");
        }

        if (!passwordVerifier.Verify(userAccount, request.Password))
        {
            return LoginResult.Failure("Invalid email or password.");
        }

        var token = tokenService.CreateToken(userAccount);
        var session = tokenService.ValidateToken(token);
        if (session is null)
        {
            return LoginResult.Failure("Authentication service unavailable. Try again later.");
        }

        var refreshToken = tokenService.CreateRefreshToken(userAccount);
        return LoginResult.Success(token, Enrich(session), refreshToken);
    }

    private SessionState Enrich(SessionState session)
    {
        return new SessionState
        {
            Email = session.Email,
            DisplayName = session.DisplayName,
            ExpiresAtUtc = session.ExpiresAtUtc,
            AuthenticationSource = cognitoOptions.AuthenticationSource,
            MfaRequired = cognitoOptions.RequireMfa,
        };
    }

    private SessionState BuildSessionFromTokens(string accessToken, string? idToken)
    {
        var accessJwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        JwtSecurityToken? idJwt = null;
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            idJwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        }

        var email =
            idJwt
                ?.Claims.FirstOrDefault(claim =>
                    string.Equals(claim.Type, "email", StringComparison.OrdinalIgnoreCase)
                )
                ?.Value
            ?? accessJwt
                .Claims.FirstOrDefault(claim =>
                    string.Equals(claim.Type, "username", StringComparison.OrdinalIgnoreCase)
                )
                ?.Value
            ?? string.Empty;
        var displayName =
            idJwt
                ?.Claims.FirstOrDefault(claim =>
                    string.Equals(claim.Type, "name", StringComparison.OrdinalIgnoreCase)
                )
                ?.Value ?? email;

        return new SessionState
        {
            Email = email,
            DisplayName = displayName,
            ExpiresAtUtc = new DateTimeOffset(accessJwt.ValidTo, TimeSpan.Zero),
            AuthenticationSource = cognitoOptions.AuthenticationSource,
            MfaRequired = cognitoOptions.RequireMfa,
        };
    }

    private SessionState BuildSessionFromAccessToken(
        string accessToken,
        string email,
        string displayName
    )
    {
        var accessJwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return new SessionState
        {
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            ExpiresAtUtc = new DateTimeOffset(accessJwt.ValidTo, TimeSpan.Zero),
            AuthenticationSource = cognitoOptions.AuthenticationSource,
            MfaRequired = cognitoOptions.RequireMfa,
        };
    }
}
