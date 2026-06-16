using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Identity;
using CashFlow.Auth.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.UnitTests.Application;

public sealed class InMemoryUserAdministrationTests
{
    [Fact]
    public async Task UpsertUserAsync_CreatesUserThatCanAuthenticate()
    {
        var passwordVerifier = new PasswordVerifier();
        var store = new InMemoryUserAccountStore(passwordVerifier);
        var administration = new InMemoryUserAdministrationService(store);
        var provider = new CognitoIdentityProvider(
            new NoOpCognitoIdentityGateway(),
            store,
            passwordVerifier,
            new JwtTokenService(Options.Create(new JwtOptions())),
            new LocalMfaChallengeStore(),
            Options.Create(new CognitoOptions { Enabled = false, RequireMfa = false }),
            Options.Create(new LocalAuthOptions()));

        await administration.UpsertUserAsync(new CashFlow.Auth.Application.Contracts.UserSummary
        {
            Email = "analyst@cashflow.local",
            DisplayName = "Analyst",
            IsActive = true
        });

        var result = await provider.AuthenticateAsync(new CashFlow.Auth.Application.Contracts.LoginRequest
        {
            Email = "analyst@cashflow.local",
            Password = "Pass@word1"
        });

        Assert.True(result.Succeeded);
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
