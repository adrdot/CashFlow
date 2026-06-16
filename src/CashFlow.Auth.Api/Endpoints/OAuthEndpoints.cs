using Aspire.CashFlow.ServiceDefaults.Authentication;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.OAuth.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.Api.Endpoints;

public static class OAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/auth/oauth/authorize",
                (
                    ICognitoOAuthService oauthService,
                    IOptions<CognitoOptions> cognitoOptions,
                    IOptions<CognitoOAuthOptions> oauthOptions,
                    HttpContext httpContext,
                    string redirect_uri,
                    string state,
                    string? client_id,
                    string response_type
                ) =>
                {
                    if (!oauthService.IsEnabled)
                    {
                        return Results.Problem(
                            title: "OAuth is disabled",
                            detail: "OAuth2 authorization-code flow is not enabled for this environment.",
                            statusCode: StatusCodes.Status503ServiceUnavailable
                        );
                    }

                    if (!string.Equals(response_type, "code", StringComparison.Ordinal))
                    {
                        return Results.Problem(
                            title: "Unsupported response type",
                            detail: "Only response_type=code is supported.",
                            statusCode: StatusCodes.Status400BadRequest
                        );
                    }

                    if (string.IsNullOrWhiteSpace(redirect_uri) || string.IsNullOrWhiteSpace(state))
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: "redirect_uri and state are required.",
                            statusCode: StatusCodes.Status400BadRequest
                        );
                    }

                    var resolvedRedirectUri = ResolveRedirectUri(redirect_uri, oauthOptions.Value);
                    var authorizeUrl = oauthService.BuildAuthorizeUrl(
                        resolvedRedirectUri,
                        state,
                        client_id ?? cognitoOptions.Value.ClientId
                    );

                    if (oauthOptions.Value.UseDevHostedUi && authorizeUrl.StartsWith('/'))
                    {
                        var request = httpContext.Request;
                        authorizeUrl = $"{request.Scheme}://{request.Host}{authorizeUrl}";
                    }

                    return Results.Redirect(authorizeUrl);
                }
            )
            .WithName("OAuthAuthorize")
            .WithSummary("Starts the OAuth2 authorization-code flow.");

        app.MapGet(
                "/api/auth/oauth/login",
                (
                    ICognitoOAuthService oauthService,
                    IOptions<CognitoOAuthOptions> oauthOptions,
                    string redirect_uri,
                    string state,
                    string client_id,
                    string? scope,
                    string? error
                ) =>
                {
                    if (!oauthService.IsEnabled || !oauthOptions.Value.UseDevHostedUi)
                    {
                        return Results.Problem(
                            title: "Hosted UI unavailable",
                            detail: "The development Hosted UI is not enabled.",
                            statusCode: StatusCodes.Status404NotFound
                        );
                    }

                    return Results.Content(
                        BuildDevHostedUiHtml(redirect_uri, state, client_id, scope, error),
                        "text/html"
                    );
                }
            )
            .WithName("OAuthDevHostedUi")
            .WithSummary("Development Hosted UI for OAuth2 authorization-code flow.");

        app.MapPost(
                "/api/auth/oauth/login",
                async (
                    ICognitoOAuthService oauthService,
                    IOptions<CognitoOAuthOptions> oauthOptions,
                    HttpContext httpContext,
                    CancellationToken cancellationToken
                ) =>
                {
                    if (!oauthService.IsEnabled || !oauthOptions.Value.UseDevHostedUi)
                    {
                        return Results.Problem(
                            title: "Hosted UI unavailable",
                            detail: "The development Hosted UI is not enabled.",
                            statusCode: StatusCodes.Status404NotFound
                        );
                    }

                    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                    var redirectUri = form["redirect_uri"].ToString();
                    var state = form["state"].ToString();
                    var clientId = form["client_id"].ToString();
                    var email = form["email"].ToString();
                    var password = form["password"].ToString();
                    var mfaCode = form["mfa_code"].ToString();
                    var challengeSession = form["challenge_session"].ToString();
                    var challengeName = form["challenge_name"].ToString();

                    if (
                        string.IsNullOrWhiteSpace(redirectUri)
                        || string.IsNullOrWhiteSpace(state)
                        || string.IsNullOrWhiteSpace(clientId)
                    )
                    {
                        return Results.Content(
                            BuildDevHostedUiHtml(
                                redirectUri,
                                state,
                                clientId,
                                null,
                                "Missing OAuth parameters."
                            ),
                            "text/html",
                            statusCode: StatusCodes.Status400BadRequest
                        );
                    }

                    var resolvedRedirectUri = ResolveRedirectUri(redirectUri, oauthOptions.Value);
                    var result = await oauthService.AuthorizeDevLoginAsync(
                        new LoginRequest
                        {
                            Email = email,
                            Password = password,
                            MfaCode = string.IsNullOrWhiteSpace(mfaCode) ? null : mfaCode,
                            ChallengeSession = string.IsNullOrWhiteSpace(challengeSession)
                                ? null
                                : challengeSession,
                            ChallengeName = string.IsNullOrWhiteSpace(challengeName)
                                ? null
                                : challengeName,
                        },
                        resolvedRedirectUri,
                        state,
                        clientId,
                        cancellationToken
                    );

                    if (result.Succeeded && !string.IsNullOrWhiteSpace(result.RedirectUrl))
                    {
                        return Results.Redirect(result.RedirectUrl);
                    }

                    var loginResult =
                        result.LoginResult ?? LoginResult.Failure("Authentication failed.");
                    var errorMessage = loginResult.RequiresMfa
                        ? loginResult.ErrorMessage ?? "Enter your MFA code."
                        : loginResult.ErrorMessage ?? "Invalid email or password.";

                    return Results.Content(
                        BuildDevHostedUiHtml(
                            redirectUri,
                            state,
                            clientId,
                            null,
                            errorMessage,
                            loginResult.RequiresMfa,
                            loginResult.ChallengeSession,
                            loginResult.ChallengeName,
                            email
                        ),
                        "text/html",
                        statusCode: loginResult.RequiresMfa
                            ? StatusCodes.Status200OK
                            : StatusCodes.Status401Unauthorized
                    );
                }
            )
            .DisableAntiforgery()
            .WithName("OAuthDevHostedUiSubmit")
            .WithSummary("Authenticates through the development Hosted UI.");

        app.MapPost(
                "/api/auth/oauth/token",
                async (
                    ICognitoOAuthService oauthService,
                    OAuthTokenRequest request,
                    CancellationToken cancellationToken
                ) =>
                {
                    if (!oauthService.IsEnabled)
                    {
                        return Results.Problem(
                            title: "OAuth is disabled",
                            detail: "OAuth2 token exchange is not enabled for this environment.",
                            statusCode: StatusCodes.Status503ServiceUnavailable
                        );
                    }

                    if (
                        string.IsNullOrWhiteSpace(request.Code)
                        || string.IsNullOrWhiteSpace(request.RedirectUri)
                    )
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: "code and redirect_uri are required.",
                            statusCode: StatusCodes.Status400BadRequest
                        );
                    }

                    var tokenResponse = await oauthService.ExchangeAuthorizationCodeAsync(
                        request,
                        cancellationToken
                    );
                    if (
                        tokenResponse is null
                        || string.IsNullOrWhiteSpace(tokenResponse.AccessToken)
                    )
                    {
                        return Results.Problem(
                            title: "Invalid authorization code",
                            detail: "The authorization code is invalid, expired, or already used.",
                            statusCode: StatusCodes.Status400BadRequest
                        );
                    }

                    return Results.Ok(tokenResponse);
                }
            )
            .WithName("OAuthToken")
            .WithSummary("Exchanges an OAuth2 authorization code for access and refresh tokens.");

        return app;
    }

    private static string ResolveRedirectUri(string redirectUri, CognitoOAuthOptions oauthOptions)
    {
        if (
            !string.IsNullOrWhiteSpace(oauthOptions.RedirectUri)
            && string.Equals(
                redirectUri,
                oauthOptions.RedirectUri,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return oauthOptions.RedirectUri;
        }

        return redirectUri;
    }

    private static string BuildDevHostedUiHtml(
        string redirectUri,
        string state,
        string clientId,
        string? scope,
        string? error,
        bool isMfaStep = false,
        string? challengeSession = null,
        string? challengeName = null,
        string? email = null
    )
    {
        var encodedRedirectUri = System.Net.WebUtility.HtmlEncode(redirectUri);
        var encodedState = System.Net.WebUtility.HtmlEncode(state);
        var encodedClientId = System.Net.WebUtility.HtmlEncode(clientId);
        var encodedScope = System.Net.WebUtility.HtmlEncode(scope ?? "openid email profile");
        var encodedChallengeSession = System.Net.WebUtility.HtmlEncode(
            challengeSession ?? string.Empty
        );
        var encodedChallengeName = System.Net.WebUtility.HtmlEncode(challengeName ?? string.Empty);
        var encodedEmail = System.Net.WebUtility.HtmlEncode(email ?? string.Empty);
        var alert = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"""<div class="alert">{System.Net.WebUtility.HtmlEncode(error)}</div>""";

        if (isMfaStep)
        {
            return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>CashFlow Hosted UI - MFA</title>
  <style>
    body { font-family: system-ui, sans-serif; background: #f5f7fb; margin: 0; padding: 2rem; }
    .card { max-width: 420px; margin: 0 auto; background: #fff; border-radius: 12px; padding: 2rem; box-shadow: 0 10px 30px rgba(15, 23, 42, 0.08); }
    h1 { font-size: 1.5rem; margin: 0 0 0.5rem; }
    p { color: #475569; }
    label { display: block; font-weight: 600; margin-bottom: 0.35rem; }
    input { width: 100%; padding: 0.75rem; border: 1px solid #cbd5e1; border-radius: 8px; margin-bottom: 1rem; box-sizing: border-box; }
    button { width: 100%; padding: 0.85rem; border: 0; border-radius: 8px; background: #2563eb; color: #fff; font-weight: 600; cursor: pointer; }
    .alert { background: #fee2e2; color: #991b1b; padding: 0.75rem 1rem; border-radius: 8px; margin-bottom: 1rem; }
  </style>
</head>
<body>
  <div class="card">
    <h1>Verify MFA</h1>
    <p>Enter the MFA code for your CashFlow account.</p>
    {{alert}}
    <form method="post" action="/api/auth/oauth/login">
      <input type="hidden" name="redirect_uri" value="{{encodedRedirectUri}}" />
      <input type="hidden" name="state" value="{{encodedState}}" />
      <input type="hidden" name="client_id" value="{{encodedClientId}}" />
      <input type="hidden" name="scope" value="{{encodedScope}}" />
      <input type="hidden" name="email" value="{{encodedEmail}}" />
      <input type="hidden" name="challenge_session" value="{{encodedChallengeSession}}" />
      <input type="hidden" name="challenge_name" value="{{encodedChallengeName}}" />
      <label for="mfa_code">MFA code</label>
      <input id="mfa_code" name="mfa_code" autocomplete="one-time-code" required />
      <button type="submit">Continue</button>
    </form>
  </div>
</body>
</html>
""";
        }

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>CashFlow Hosted UI</title>
  <style>
    body { font-family: system-ui, sans-serif; background: #f5f7fb; margin: 0; padding: 2rem; }
    .card { max-width: 420px; margin: 0 auto; background: #fff; border-radius: 12px; padding: 2rem; box-shadow: 0 10px 30px rgba(15, 23, 42, 0.08); }
    h1 { font-size: 1.5rem; margin: 0 0 0.5rem; }
    p { color: #475569; }
    label { display: block; font-weight: 600; margin-bottom: 0.35rem; }
    input { width: 100%; padding: 0.75rem; border: 1px solid #cbd5e1; border-radius: 8px; margin-bottom: 1rem; box-sizing: border-box; }
    button { width: 100%; padding: 0.85rem; border: 0; border-radius: 8px; background: #2563eb; color: #fff; font-weight: 600; cursor: pointer; }
    .alert { background: #fee2e2; color: #991b1b; padding: 0.75rem 1rem; border-radius: 8px; margin-bottom: 1rem; }
    .badge { display: inline-block; background: #dbeafe; color: #1d4ed8; padding: 0.25rem 0.5rem; border-radius: 999px; font-size: 0.75rem; margin-bottom: 1rem; }
  </style>
</head>
<body>
  <div class="card">
    <span class="badge">OAuth2 authorization-code</span>
    <h1>Sign in to CashFlow</h1>
    <p>Development Hosted UI for local OAuth2 testing.</p>
    {{alert}}
    <form method="post" action="/api/auth/oauth/login">
      <input type="hidden" name="redirect_uri" value="{{encodedRedirectUri}}" />
      <input type="hidden" name="state" value="{{encodedState}}" />
      <input type="hidden" name="client_id" value="{{encodedClientId}}" />
      <input type="hidden" name="scope" value="{{encodedScope}}" />
      <label for="email">Email</label>
      <input id="email" name="email" type="email" value="{{encodedEmail}}" autocomplete="username" required />
      <label for="password">Password</label>
      <input id="password" name="password" type="password" autocomplete="current-password" required />
      <button type="submit">Sign in</button>
    </form>
  </div>
</body>
</html>
""";
    }
}
