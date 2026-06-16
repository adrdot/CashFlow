using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.Infrastructure.OAuth;

public sealed class CognitoOAuthService(
    IOptions<CognitoOptions> cognitoOptions,
    IOptions<CognitoOAuthOptions> oauthOptions,
    IAuthenticationService authenticationService,
    DevAuthorizationCodeStore authorizationCodeStore,
    IHttpClientFactory httpClientFactory) : ICognitoOAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CognitoOptions cognito = cognitoOptions.Value;
    private readonly CognitoOAuthOptions oauth = oauthOptions.Value;

    public bool IsEnabled => oauth.Enabled && (cognito.IsConfigured || oauth.UseDevHostedUi);

    public string BuildAuthorizeUrl(string redirectUri, string state, string? clientId = null)
    {
        var resolvedClientId = ResolveClientId(clientId);
        var scope = string.Join(' ', oauth.Scopes.Length == 0 ? ["openid", "email", "profile"] : oauth.Scopes);

        if (oauth.UseAwsHostedUi)
        {
            var query = new Dictionary<string, string?>
            {
                ["client_id"] = resolvedClientId,
                ["response_type"] = "code",
                ["scope"] = scope,
                ["redirect_uri"] = redirectUri,
                ["state"] = state
            };

            return QueryHelpersAppend($"{HostedUiBaseUrl}/oauth2/authorize", query);
        }

        var devQuery = new Dictionary<string, string?>
        {
            ["client_id"] = resolvedClientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["scope"] = scope
        };

        return QueryHelpersAppend("/api/auth/oauth/login", devQuery);
    }

    public async Task<OAuthAuthorizationResult> AuthorizeDevLoginAsync(
        LoginRequest request,
        string redirectUri,
        string state,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        if (!oauth.UseDevHostedUi)
        {
            return OAuthAuthorizationResult.Pending(LoginResult.Failure("OAuth dev Hosted UI is not enabled."));
        }

        var loginResult = await authenticationService.LoginAsync(request, cancellationToken);
        if (!loginResult.Succeeded || string.IsNullOrWhiteSpace(loginResult.Token))
        {
            return OAuthAuthorizationResult.Pending(loginResult);
        }

        var code = authorizationCodeStore.Create(
            loginResult,
            redirectUri,
            ResolveClientId(clientId),
            state,
            TimeSpan.FromMinutes(5));

        var redirectUrl = BuildCallbackRedirect(redirectUri, code, state);
        return OAuthAuthorizationResult.Redirect(redirectUrl);
    }

    public async Task<OAuthTokenResponse?> ExchangeAuthorizationCodeAsync(
        OAuthTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || !string.Equals(request.GrantType, "authorization_code", StringComparison.Ordinal))
        {
            return null;
        }

        if (oauth.UseAwsHostedUi)
        {
            return await ExchangeAwsAuthorizationCodeAsync(request, cancellationToken);
        }

        if (authorizationCodeStore.TryRedeem(
                request.Code,
                request.RedirectUri,
                ResolveClientId(request.ClientId),
                out var loginResult)
            && loginResult is not null)
        {
            return OAuthTokenResponse.FromLoginResult(loginResult);
        }

        return null;
    }

    private async Task<OAuthTokenResponse?> ExchangeAwsAuthorizationCodeAsync(
        OAuthTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oauth.ClientSecret))
        {
            return null;
        }

        using var httpClient = httpClientFactory.CreateClient(nameof(CognitoOAuthService));
        using var payload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ResolveClientId(request.ClientId),
            ["client_secret"] = oauth.ClientSecret,
            ["code"] = request.Code,
            ["redirect_uri"] = request.RedirectUri
        });

        using var response = await httpClient.PostAsync($"{HostedUiBaseUrl}/oauth2/token", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var tokenPayload = await JsonSerializer.DeserializeAsync<CognitoTokenPayload>(stream, JsonOptions, cancellationToken);
        if (tokenPayload is null || string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
        {
            return null;
        }

        var session = await authenticationService.ValidateSessionAsync(tokenPayload.AccessToken, cancellationToken);
        return new OAuthTokenResponse
        {
            AccessToken = tokenPayload.AccessToken,
            RefreshToken = tokenPayload.RefreshToken,
            IdToken = tokenPayload.IdToken,
            ExpiresIn = tokenPayload.ExpiresIn,
            Session = session
        };
    }

    private string HostedUiBaseUrl =>
        $"https://{oauth.Domain.Trim()}.auth.{cognito.Region}.amazoncognito.com";

    private string ResolveClientId(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? cognito.ClientId : clientId;

    private static string QueryHelpersAppend(string baseUrl, IReadOnlyDictionary<string, string?> query)
    {
        var builder = new StringBuilder(baseUrl);
        builder.Append(baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?');

        var first = true;
        foreach (var (key, value) in query)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!first)
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value));
            first = false;
        }

        return builder.ToString();
    }

    private static string BuildCallbackRedirect(string redirectUri, string code, string state)
    {
        var separator = redirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}";
    }

    private sealed class CognitoTokenPayload
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; init; }
    }
}
