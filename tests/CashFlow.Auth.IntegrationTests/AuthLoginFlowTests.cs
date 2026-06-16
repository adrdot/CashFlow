using System.Net;
using System.Net.Http.Json;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.ContractTests;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CashFlow.Auth.IntegrationTests;

public sealed class AuthLoginFlowTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory factory;

    public AuthLoginFlowTests(AuthWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_ReturnsMfaChallenge_ThenIssuesToken_WhenMfaCodeIsProvided()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var challengeResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "admin@cashflow.local",
            Password = "Pass@word1"
        });

        Assert.Equal(HttpStatusCode.OK, challengeResponse.StatusCode);
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(challenge);
        Assert.True(challenge.RequiresMfa);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "admin@cashflow.local",
            Password = "Pass@word1",
            ChallengeSession = challenge.ChallengeSession,
            ChallengeName = challenge.ChallengeName,
            MfaCode = "123456"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(loginResult);
        Assert.True(loginResult.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token));
        Assert.False(string.IsNullOrWhiteSpace(loginResult.RefreshToken));

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = loginResult.RefreshToken!
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(refreshResult);
        Assert.True(refreshResult.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.Token));
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.RefreshToken));
        Assert.NotEqual(loginResult.RefreshToken, refreshResult.RefreshToken);
    }

    [Fact]
    public async Task Session_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
