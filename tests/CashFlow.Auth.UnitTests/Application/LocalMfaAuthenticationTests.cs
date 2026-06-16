using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Identity;
using CashFlow.Auth.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.UnitTests.Application;

public sealed class LocalMfaAuthenticationTests
{
    [Fact]
    public async Task AuthenticateAsync_ReturnsMfaChallenge_WhenRequireMfaIsEnabled()
    {
        var provider = CreateProvider(requireMfa: true);

        var result = await provider.AuthenticateAsync(new CashFlow.Auth.Application.Contracts.LoginRequest
        {
            Email = "admin@cashflow.local",
            Password = "Pass@word1"
        });

        Assert.True(result.RequiresMfa);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ChallengeSession);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsToken_WhenMfaCodeIsValid()
    {
        var provider = CreateProvider(requireMfa: true);

        var challenge = await provider.AuthenticateAsync(new CashFlow.Auth.Application.Contracts.LoginRequest
        {
            Email = "admin@cashflow.local",
            Password = "Pass@word1"
        });

        var result = await provider.AuthenticateAsync(new CashFlow.Auth.Application.Contracts.LoginRequest
        {
            Email = "admin@cashflow.local",
            Password = "Pass@word1",
            ChallengeSession = challenge.ChallengeSession,
            ChallengeName = challenge.ChallengeName,
            MfaCode = "123456"
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Token);
    }

    private static CognitoIdentityProvider CreateProvider(bool requireMfa)
    {
        var passwordVerifier = new PasswordVerifier();
        var userAccountStore = new InMemoryUserAccountStore(passwordVerifier);
        var tokenService = new JwtTokenService(Options.Create(new JwtOptions()));
        var cognitoIdentityGateway = new NoOpCognitoIdentityGateway();

        return new CognitoIdentityProvider(
            cognitoIdentityGateway,
            userAccountStore,
            passwordVerifier,
            tokenService,
            new LocalMfaChallengeStore(),
            Options.Create(new CognitoOptions
            {
                Enabled = false,
                AuthenticationSource = "InMemoryFallback",
                RequireMfa = requireMfa
            }),
            Options.Create(new LocalAuthOptions()));
    }

    private sealed class NoOpCognitoIdentityGateway : ICognitoIdentityGateway
    {
        public Task<CognitoAuthResult> AuthenticateAsync(string clientId, string username, string password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException();

        public Task<CognitoAuthResult> RespondToMfaChallengeAsync(string clientId, string challengeSession, string username, string mfaCode, string challengeName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException();

        public Task<CognitoAuthResult> RefreshTokenAsync(string clientId, string refreshToken, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException();

        public Task<CognitoUserProfile?> GetUserAsync(string accessToken, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException();

        public Task GlobalSignOutAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
