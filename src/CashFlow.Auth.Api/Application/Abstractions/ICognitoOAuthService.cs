using CashFlow.Auth.Application.Contracts;

namespace CashFlow.Auth.Application.Abstractions;

public interface ICognitoOAuthService
{
    bool IsEnabled { get; }

    string BuildAuthorizeUrl(string redirectUri, string state, string? clientId = null);

    Task<OAuthAuthorizationResult> AuthorizeDevLoginAsync(
        LoginRequest request,
        string redirectUri,
        string state,
        string clientId,
        CancellationToken cancellationToken = default);

    Task<OAuthTokenResponse?> ExchangeAuthorizationCodeAsync(
        OAuthTokenRequest request,
        CancellationToken cancellationToken = default);
}
