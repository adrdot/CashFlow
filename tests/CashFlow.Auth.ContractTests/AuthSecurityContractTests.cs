using System.Net;
using System.Net.Http.Json;
using CashFlow.Auth.Application.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CashFlow.Auth.ContractTests;

public sealed class AuthSecurityContractTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory factory;

    public AuthSecurityContractTests(AuthWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Login_ReturnsProblemDetails_WhenCredentialsAreMissing()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "", Password = "" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_ReturnsMfaChallengeContract_WhenPasswordIsValid()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "admin@cashflow.local", Password = "Pass@word1" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResult>();

        Assert.NotNull(payload);
        Assert.True(payload.RequiresMfa);
        Assert.NotNull(payload.ChallengeSession);
        Assert.NotNull(payload.ChallengeName);
    }
}
