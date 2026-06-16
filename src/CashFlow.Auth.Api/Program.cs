using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Aspire.CashFlow.ServiceDefaults;
using Aspire.CashFlow.ServiceDefaults.Authentication;
using Aspire.CashFlow.ServiceDefaults.Security;
using CashFlow.Auth.Api.Endpoints;
using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Application.UseCases;
using CashFlow.Auth.Infrastructure;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Identity.Abstractions;
using CashFlow.Auth.Infrastructure.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var secretsManagerSection = builder.Configuration.GetSection(SecretsManagerOptions.SectionName);
var kmsSection = builder.Configuration.GetSection(KmsOptions.SectionName);

builder.AddServiceDefaults();
builder.AddCashFlowRuntimeSecrets();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.Configure<SecretsManagerOptions>(secretsManagerSection);
builder.Services.Configure<KmsOptions>(kmsSection);
builder.Services.AddCashFlowJwtAuthentication(builder.Configuration);
builder.Services.AddAuthInfrastructure(builder.Configuration);
builder.Services.AddScoped<IAuthenticationService, LoginUserHandler>();

var app = builder.Build();
var cognitoOptions = app.Services.GetRequiredService<IOptions<CognitoOptions>>().Value;

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCashFlowApiPipeline(useCashFlowHttpsRedirection: false);
app.UseCashFlowApiAuthentication();

app.MapPost(
        "/api/auth/login",
        async (
            LoginRequest request,
            IAuthenticationService authenticationService,
            SecurityAuditService securityAuditService,
            AuthMetrics authMetrics,
            CancellationToken cancellationToken
        ) =>
        {
            if (
                string.IsNullOrWhiteSpace(request.Email)
                || string.IsNullOrWhiteSpace(request.Password)
            )
            {
                RecordLoginEvent(
                    securityAuditService,
                    authMetrics,
                    request.Email,
                    "failed",
                    "missing-credentials"
                );
                return Results.Problem(
                    title: "Invalid request",
                    detail: "Email and password are required.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var result = await authenticationService.LoginAsync(request, cancellationToken);
            if (result.RequiresMfa)
            {
                RecordLoginEvent(
                    securityAuditService,
                    authMetrics,
                    request.Email,
                    "mfa-required",
                    "challenge-issued"
                );
                return Results.Ok(result);
            }

            if (result.Succeeded)
            {
                RecordLoginEvent(
                    securityAuditService,
                    authMetrics,
                    request.Email,
                    "succeeded",
                    "token-issued"
                );
                return Results.Ok(result);
            }

            RecordLoginEvent(
                securityAuditService,
                authMetrics,
                request.Email,
                "failed",
                result.ErrorMessage ?? "invalid-credentials"
            );
            return Results.Problem(
                title: "Invalid credentials",
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
    )
    .RequireRateLimiting(CashFlowSecurityExtensions.AuthLoginRateLimitPolicy)
    .WithName("Login")
    .WithSummary("Authenticates a user and issues a JWT or MFA challenge.");

app.MapPost(
        "/api/auth/refresh",
        async (
            RefreshTokenRequest request,
            IAuthenticationService authenticationService,
            SecurityAuditService securityAuditService,
            AuthMetrics authMetrics,
            CancellationToken cancellationToken
        ) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                RecordRefreshEvent(
                    securityAuditService,
                    authMetrics,
                    "failed",
                    "missing-refresh-token"
                );
                return Results.Problem(
                    title: "Invalid request",
                    detail: "Refresh token is required.",
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var result = await authenticationService.RefreshSessionAsync(
                request.RefreshToken,
                cancellationToken
            );
            if (result.Succeeded)
            {
                RecordRefreshEvent(securityAuditService, authMetrics, "succeeded", "token-issued");
                return Results.Ok(result);
            }

            RecordRefreshEvent(
                securityAuditService,
                authMetrics,
                "failed",
                result.ErrorMessage ?? "invalid-refresh-token"
            );
            return Results.Problem(
                title: "Invalid refresh token",
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status400BadRequest
            );
        }
    )
    .RequireRateLimiting(CashFlowSecurityExtensions.AuthLoginRateLimitPolicy)
    .WithName("RefreshToken")
    .WithSummary("Refreshes an access token using a valid refresh token.");

app.MapGet(
        "/api/auth/session",
        [Authorize]
        (
            ClaimsPrincipal user,
            IOptions<CognitoOptions> options,
            SecurityAuditService securityAuditService,
            AuthMetrics authMetrics
        ) =>
        {
            var session = MapSession(user, options.Value);
            securityAuditService.Record(
                new SecurityEventRecord
                {
                    EventType = AuthMetrics.SessionValidated,
                    Outcome = "succeeded",
                    Subject = session.Email,
                }
            );

            return TypedResults.Ok(session);
        }
    )
    .WithName("GetSession")
    .WithSummary("Validates the caller JWT and returns session details.");

app.MapPost(
        "/api/auth/logout",
        [Authorize]
        async (
            HttpContext httpContext,
            IIdentityProvider identityProvider,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken
        ) =>
        {
            var email =
                httpContext.User.FindFirstValue(ClaimTypes.Email)
                ?? httpContext.User.FindFirstValue("email")
                ?? "unknown";
            var token = ExtractBearerToken(httpContext);
            if (!string.IsNullOrWhiteSpace(token))
            {
                await identityProvider.RevokeSessionAsync(token, cancellationToken);
            }

            securityAuditService.Record(
                new SecurityEventRecord
                {
                    EventType = "auth.logout",
                    Outcome = "succeeded",
                    Subject = email,
                }
            );

            return Results.NoContent();
        }
    )
    .WithName("Logout")
    .WithSummary("Revokes the current session.");

app.MapOAuthEndpoints();

if (app.Environment.IsDevelopment() && !cognitoOptions.IsConfigured)
{
    app.MapUserAdministrationEndpoints();
}

app.MapDefaultEndpoints();
app.Run();

static void RecordLoginEvent(
    SecurityAuditService securityAuditService,
    AuthMetrics authMetrics,
    string email,
    string outcome,
    string reason
)
{
    securityAuditService.Record(
        new SecurityEventRecord
        {
            EventType =
                outcome == "succeeded" ? AuthMetrics.LoginSucceeded : AuthMetrics.LoginFailed,
            Outcome = outcome,
            Subject = string.IsNullOrWhiteSpace(email) ? "unknown" : email,
            Dimensions = new Dictionary<string, string> { ["reason"] = reason },
        }
    );
}

static void RecordRefreshEvent(
    SecurityAuditService securityAuditService,
    AuthMetrics authMetrics,
    string outcome,
    string reason
)
{
    securityAuditService.Record(
        new SecurityEventRecord
        {
            EventType =
                outcome == "succeeded"
                    ? AuthMetrics.TokenRefreshSucceeded
                    : AuthMetrics.TokenRefreshFailed,
            Outcome = outcome,
            Subject = "refresh-token",
            Dimensions = new Dictionary<string, string> { ["reason"] = reason },
        }
    );
}

static SessionState MapSession(ClaimsPrincipal user, CognitoOptions cognitoOptions)
{
    var expiresAt = DateTimeOffset.UtcNow;
    var expClaim = user.FindFirstValue(JwtRegisteredClaimNames.Exp);

    if (long.TryParse(expClaim, out var expSeconds))
    {
        expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
    }

    var email =
        user.FindFirstValue(ClaimTypes.Email)
        ?? user.FindFirstValue("email")
        ?? user.FindFirstValue("username")
        ?? string.Empty;
    var displayName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name") ?? email;

    return new SessionState
    {
        Email = email,
        DisplayName = displayName,
        AuthenticationSource =
            user.FindFirstValue("auth_source")
            ?? (cognitoOptions.IsConfigured ? "Cognito" : "InMemoryFallback"),
        MfaRequired = string.Equals(
            user.FindFirstValue("mfa_required"),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase
        ),
        ExpiresAtUtc = expiresAt,
    };
}

static string? ExtractBearerToken(HttpContext httpContext)
{
    var authorizationHeader = httpContext.Request.Headers.Authorization.ToString();
    if (
        string.IsNullOrWhiteSpace(authorizationHeader)
        || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
    )
    {
        return null;
    }

    return authorizationHeader["Bearer ".Length..].Trim();
}

public partial class Program;
