using CashFlow.Web.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace CashFlow.Web.Services;

public sealed class OAuthLoginService(
    AuthApiClient authApiClient,
    IOptions<CognitoOAuthOptions> oauthOptions,
    IOptions<DemoAccountOptions> demoAccountOptions,
    IConfiguration configuration,
    NavigationManager navigationManager
)
{
    private readonly CognitoOAuthOptions oauth = oauthOptions.Value;

    public bool IsEnabled => oauth.Enabled;

    public string BuildAuthorizeUrl(string state)
    {
        var authApiBase = ResolveAuthApiPublicBaseAddress();
        var redirectUri = ResolveRedirectUri();
        var clientId = configuration["Cognito:ClientId"] ?? string.Empty;
        var scope = string.Join(
            ' ',
            oauth.Scopes.Length == 0 ? ["openid", "email", "profile"] : oauth.Scopes
        );

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["scope"] = scope,
        };

        var authorizePath = QueryString.Create(
            query.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
        );
        return $"{authApiBase.TrimEnd('/')}/api/auth/oauth/authorize{authorizePath}";
    }

    public string ResolveRedirectUri()
    {
        if (!string.IsNullOrWhiteSpace(oauth.RedirectUri))
        {
            return oauth.RedirectUri;
        }

        var baseUri = navigationManager.BaseUri.TrimEnd('/');
        return $"{baseUri}/auth/callback";
    }

    public async Task<StoredSession?> CompleteCallbackAsync(
        string code,
        CancellationToken cancellationToken = default
    )
    {
        var tokenResponse = await authApiClient.ExchangeOAuthCodeAsync(
            code,
            ResolveRedirectUri(),
            cancellationToken
        );

        if (tokenResponse?.Session is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            return null;
        }

        return new StoredSession
        {
            Token = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            Email = tokenResponse.Session.Email,
            DisplayName = tokenResponse.Session.DisplayName,
            ExpiresAtUtc = tokenResponse.Session.ExpiresAtUtc,
        };
    }

    public string DemoAccountHint => demoAccountOptions.Value.Description;

    private string ResolveAuthApiPublicBaseAddress()
    {
        var configured =
            configuration["AuthApi:PublicBaseAddress"] ?? configuration["AuthApi:BaseAddress"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured
                .Replace("https+http://", "https://", StringComparison.Ordinal)
                .TrimEnd('/');
        }

        throw new InvalidOperationException(
            "AuthApi:PublicBaseAddress or AuthApi:BaseAddress must be configured."
        );
    }
}
