using System.Net;
using System.Net.Http.Json;
using CashFlow.Auth.Application.Contracts;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.ContractTests;

public sealed class AuthOAuthContractTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> factory;

    public AuthOAuthContractTests(AuthWebApplicationFactory factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cognito:RequireMfa"] = "false"
                });
            });
        });
    }

    [Fact]
    public async Task Authorize_RedirectsToDevHostedUi_WhenOAuthIsEnabled()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(
            "/api/auth/oauth/authorize?response_type=code&client_id=local-client&redirect_uri=https://localhost:7262/auth/callback&state=test-state");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/api/auth/oauth/login", location, StringComparison.Ordinal);
        Assert.Contains("state=test-state", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizationCodeFlow_ReturnsTokens_WhenCredentialsAreValid()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        const string redirectUri = "https://localhost:7262/auth/callback";
        const string state = "contract-test-state";

        using var loginResponse = await client.PostAsync("/api/auth/oauth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["client_id"] = "local-client",
            ["email"] = "admin@cashflow.local",
            ["password"] = "Pass@word1"
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        var callbackUri = loginResponse.Headers.Location!;
        var query = QueryHelpers.ParseQuery(callbackUri.Query);
        query.TryGetValue("code", out var codeValues);
        query.TryGetValue("state", out var stateValues);
        var code = codeValues.FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.Equal(state, stateValues.FirstOrDefault());

        using var tokenResponse = await client.PostAsJsonAsync("/api/auth/oauth/token", new OAuthTokenRequest
        {
            GrantType = "authorization_code",
            Code = code!,
            RedirectUri = redirectUri,
            ClientId = "local-client"
        });

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var payload = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.NotNull(payload.Session);
        Assert.Equal("admin@cashflow.local", payload.Session.Email);
    }
}
