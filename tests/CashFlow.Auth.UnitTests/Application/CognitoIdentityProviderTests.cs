using Aspire.CashFlow.ServiceDefaults.Authentication;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Identity;
using CashFlow.Auth.Infrastructure.Identity.Abstractions;
using CashFlow.Auth.Infrastructure.Security;
using CashFlow.Auth.Infrastructure.Security.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.UnitTests.Application;

internal static class TestLocalAuthOptions
{
    public static IOptions<LocalAuthOptions> Create() =>
        Options.Create(
            new LocalAuthOptions
            {
                MfaCode = "123456",
                MfaChallengeTtlMinutes = 5,
                SeedAdminEmail = "admin@cashflow.local",
                SeedAdminPassword = "Pass@word1",
                SeedAdminUserId = "b2f02e39-71d0-4d73-96df-f8626776f2a4",
                SeedAdminDisplayName = "Cash Flow Admin",
                DefaultUserPassword = "Pass@word1",
            }
        );
}

internal static class TestJwtOptions
{
    public const string SigningKey = "dev-only-signing-key-change-me-1234567890";

    public static IOptions<JwtOptions> Create() =>
        Options.Create(
            new JwtOptions
            {
                Issuer = "CashFlow.Auth.Api",
                Audience = "CashFlow.Web",
                SigningKey = SigningKey,
                ExpirationMinutes = 60,
                RefreshTokenExpirationDays = 7,
                ClockSkewSeconds = 30,
            }
        );
}

public sealed class CognitoIdentityProviderTests
{
    private readonly PasswordVerifier passwordVerifier = new();
    private readonly InMemoryUserAccountStore userAccountStore;
    private readonly JwtTokenService tokenService;
    private readonly LocalMfaChallengeStore localMfaChallengeStore = new();
    private readonly ICognitoIdentityGateway cognitoIdentityGateway =
        new NoOpCognitoIdentityGateway();

    public CognitoIdentityProviderTests()
    {
        userAccountStore = new InMemoryUserAccountStore(
            passwordVerifier,
            TestLocalAuthOptions.Create()
        );
        tokenService = new JwtTokenService(TestJwtOptions.Create());
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsMfaChallenge_WhenRequireMfaIsEnabled()
    {
        var provider = CreateProvider(requireMfa: true);

        var result = await provider.AuthenticateAsync(
            new LoginRequest { Email = "admin@cashflow.local", Password = "Pass@word1" }
        );

        Assert.True(result.RequiresMfa);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ChallengeSession);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsTokenAndEnrichedSession_WhenCredentialsAreValid()
    {
        var provider = CreateProvider(requireMfa: false);

        var result = await provider.AuthenticateAsync(
            new LoginRequest { Email = "admin@cashflow.local", Password = "Pass@word1" }
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Token);
        Assert.NotNull(result.Session);
        Assert.Equal("InMemoryFallback", result.Session!.AuthenticationSource);
        Assert.False(result.Session.MfaRequired);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsFailure_WhenCredentialsAreInvalid()
    {
        var provider = CreateProvider(requireMfa: false);

        var result = await provider.AuthenticateAsync(
            new LoginRequest { Email = "admin@cashflow.local", Password = "wrong-password" }
        );

        Assert.False(result.Succeeded);
        Assert.Null(result.Token);
        Assert.Equal("Invalid email or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsFailure_WhenCognitoRejectsCredentials()
    {
        var provider = CreateCognitoProvider(
            new FakeCognitoIdentityGateway(new CognitoAuthResult { IsInvalidCredentials = true })
        );

        var result = await provider.AuthenticateAsync(
            new LoginRequest { Email = "admin@cashflow.docker", Password = "wrong-password" }
        );

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid email or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsRefreshToken_WhenCredentialsAreValid()
    {
        var provider = CreateProvider(requireMfa: false);

        var result = await provider.AuthenticateAsync(
            new LoginRequest { Email = "admin@cashflow.local", Password = "Pass@word1" }
        );

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
    }

    [Fact]
    public async Task RefreshSessionAsync_ReturnsNewTokens_WhenRefreshTokenIsValid()
    {
        var provider = CreateProvider(requireMfa: false);
        var loginResult = await provider.AuthenticateAsync(
            new LoginRequest { Email = "admin@cashflow.local", Password = "Pass@word1" }
        );

        var refreshResult = await provider.RefreshSessionAsync(loginResult.RefreshToken!);
        Assert.True(refreshResult.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.Token));
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.RefreshToken));
        Assert.NotEqual(loginResult.RefreshToken, refreshResult.RefreshToken);
    }

    [Fact]
    public async Task RefreshSessionAsync_ReturnsFailure_WhenCognitoRefreshIsRejected()
    {
        var provider = CreateCognitoProvider(
            new FakeCognitoIdentityGateway(new CognitoAuthResult { IsInvalidRefreshToken = true })
        );

        var result = await provider.RefreshSessionAsync("stale-refresh-token");
        Assert.False(result.Succeeded);
        Assert.Equal("Refresh token is invalid or expired.", result.ErrorMessage);
    }

    [Fact]
    public async Task RefreshSessionAsync_ReturnsNewAccessToken_WhenCognitoRefreshSucceeds()
    {
        var accessToken = CreateJwt("access-token", DateTime.UtcNow.AddMinutes(30));
        var provider = CreateCognitoProvider(
            new FakeCognitoIdentityGateway(
                new CognitoAuthResult
                {
                    AccessToken = accessToken,
                    IdToken = CreateJwt(
                        "id-token",
                        DateTime.UtcNow.AddMinutes(30),
                        email: "admin@cashflow.docker"
                    ),
                    RefreshToken = "rotated-refresh-token",
                }
            )
        );

        var result = await provider.RefreshSessionAsync("existing-refresh-token");
        Assert.True(result.Succeeded);
        Assert.Equal(accessToken, result.Token);
        Assert.Equal("rotated-refresh-token", result.RefreshToken);
    }

    private CognitoIdentityProvider CreateProvider(bool requireMfa)
    {
        return new CognitoIdentityProvider(
            cognitoIdentityGateway,
            userAccountStore,
            passwordVerifier,
            tokenService,
            localMfaChallengeStore,
            Options.Create(
                new CognitoOptions
                {
                    Enabled = false,
                    AuthenticationSource = "InMemoryFallback",
                    RequireMfa = requireMfa,
                }
            ),
            TestLocalAuthOptions.Create()
        );
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsLocalMfaChallenge_WhenCognitoLocalReturnsTokensDirectly()
    {
        var accessToken = CreateJwt("access-token", DateTime.UtcNow.AddMinutes(30));
        var gateway = new FakeCognitoIdentityGateway(
            new CognitoAuthResult
            {
                AccessToken = accessToken,
                IdToken = CreateJwt(
                    "id-token",
                    DateTime.UtcNow.AddMinutes(30),
                    email: "admin@cashflow.docker"
                ),
                RefreshToken = "refresh-token",
            }
        );
        var provider = CreateCognitoProvider(
            gateway,
            serviceUrl: "http://localhost:9229",
            requireMfa: true
        );

        var result = await provider.AuthenticateAsync(
            new LoginRequest { Email = "admin@cashflow.docker", Password = "Pass@word1" }
        );

        Assert.True(result.RequiresMfa);
        Assert.Equal("LOCAL_MFA", result.ChallengeName);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsCognitoTokens_WhenLocalMfaCodeIsValid()
    {
        var accessToken = CreateJwt("access-token", DateTime.UtcNow.AddMinutes(30));
        var gateway = new FakeCognitoIdentityGateway(
            new CognitoAuthResult
            {
                AccessToken = accessToken,
                IdToken = CreateJwt(
                    "id-token",
                    DateTime.UtcNow.AddMinutes(30),
                    email: "admin@cashflow.docker"
                ),
                RefreshToken = "refresh-token",
            }
        );
        var provider = CreateCognitoProvider(
            gateway,
            serviceUrl: "http://localhost:9229",
            requireMfa: true
        );

        var challenge = await provider.AuthenticateAsync(
            new LoginRequest { Email = "admin@cashflow.docker", Password = "Pass@word1" }
        );

        var result = await provider.AuthenticateAsync(
            new LoginRequest
            {
                Email = "admin@cashflow.docker",
                Password = "Pass@word1",
                ChallengeSession = challenge.ChallengeSession,
                ChallengeName = challenge.ChallengeName,
                MfaCode = "123456",
            }
        );

        Assert.True(result.Succeeded);
        Assert.Equal(accessToken, result.Token);
        Assert.Equal("refresh-token", result.RefreshToken);
    }

    [Fact]
    public async Task RevokeSessionAsync_SkipsGlobalSignOut_WhenCognitoLocalEndpointIsConfigured()
    {
        var gateway = new RecordingCognitoIdentityGateway();
        var provider = CreateCognitoProvider(gateway, serviceUrl: "http://localhost:9229");

        await provider.RevokeSessionAsync("access-token");

        Assert.False(gateway.GlobalSignOutCalled);
    }

    [Fact]
    public async Task RevokeSessionAsync_CallsGlobalSignOut_WhenUsingAwsCognito()
    {
        var gateway = new RecordingCognitoIdentityGateway();
        var provider = CreateCognitoProvider(gateway);

        await provider.RevokeSessionAsync("access-token");

        Assert.True(gateway.GlobalSignOutCalled);
        Assert.Equal("access-token", gateway.GlobalSignOutAccessToken);
    }

    private CognitoIdentityProvider CreateCognitoProvider(
        ICognitoIdentityGateway gateway,
        string? serviceUrl = null,
        bool requireMfa = false
    )
    {
        return new CognitoIdentityProvider(
            gateway,
            userAccountStore,
            passwordVerifier,
            tokenService,
            localMfaChallengeStore,
            Options.Create(
                new CognitoOptions
                {
                    Enabled = true,
                    ClientId = "test-client",
                    UserPoolId = "local_test",
                    Region = "us-east-1",
                    ServiceUrl = serviceUrl,
                    RequireMfa = requireMfa,
                    AuthenticationSource = serviceUrl is null ? "AwsCognito" : "CognitoLocal",
                }
            ),
            TestLocalAuthOptions.Create()
        );
    }

    private static string CreateJwt(string subject, DateTime expiresAtUtc, string? email = null)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, subject),
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(
                new System.Security.Claims.Claim(
                    System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email,
                    email
                )
            );
        }

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            claims: claims,
            expires: expiresAtUtc
        );
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class RecordingCognitoIdentityGateway : ICognitoIdentityGateway
    {
        public bool GlobalSignOutCalled { get; private set; }

        public string? GlobalSignOutAccessToken { get; private set; }

        public Task<CognitoAuthResult> AuthenticateAsync(
            string clientId,
            string username,
            string password,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new CognitoAuthResult());

        public Task<CognitoAuthResult> RespondToMfaChallengeAsync(
            string clientId,
            string challengeSession,
            string username,
            string mfaCode,
            string challengeName,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new CognitoAuthResult());

        public Task<CognitoAuthResult> RefreshTokenAsync(
            string clientId,
            string refreshToken,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new CognitoAuthResult());

        public Task<CognitoUserProfile?> GetUserAsync(
            string accessToken,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<CognitoUserProfile?>(null);

        public Task GlobalSignOutAsync(
            string accessToken,
            CancellationToken cancellationToken = default
        )
        {
            GlobalSignOutCalled = true;
            GlobalSignOutAccessToken = accessToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCognitoIdentityGateway(CognitoAuthResult result)
        : ICognitoIdentityGateway
    {
        public Task<CognitoAuthResult> AuthenticateAsync(
            string clientId,
            string username,
            string password,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(result);

        public Task<CognitoAuthResult> RespondToMfaChallengeAsync(
            string clientId,
            string challengeSession,
            string username,
            string mfaCode,
            string challengeName,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(result);

        public Task<CognitoAuthResult> RefreshTokenAsync(
            string clientId,
            string refreshToken,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(result);

        public Task<CognitoUserProfile?> GetUserAsync(
            string accessToken,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<CognitoUserProfile?>(null);

        public Task GlobalSignOutAsync(
            string accessToken,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }

    private sealed class NoOpCognitoIdentityGateway : ICognitoIdentityGateway
    {
        public Task<CognitoAuthResult> AuthenticateAsync(
            string clientId,
            string username,
            string password,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Cognito is disabled in this test.");

        public Task<CognitoAuthResult> RespondToMfaChallengeAsync(
            string clientId,
            string challengeSession,
            string username,
            string mfaCode,
            string challengeName,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Cognito is disabled in this test.");

        public Task<CognitoAuthResult> RefreshTokenAsync(
            string clientId,
            string refreshToken,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Cognito is disabled in this test.");

        public Task<CognitoUserProfile?> GetUserAsync(
            string accessToken,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("Cognito is disabled in this test.");

        public Task GlobalSignOutAsync(
            string accessToken,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
