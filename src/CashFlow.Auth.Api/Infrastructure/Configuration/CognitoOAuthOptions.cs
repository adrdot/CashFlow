namespace CashFlow.Auth.Infrastructure.Configuration;

public sealed class CognitoOAuthOptions
{
    public const string SectionName = "Cognito:OAuth";

    public bool Enabled { get; init; }

    /// <summary>
    /// Cognito Hosted UI domain prefix (e.g. "cashflow-dev" for cashflow-dev.auth.us-east-1.amazoncognito.com).
    /// When empty, the dev Hosted UI served by the auth API is used.
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    public string? ClientSecret { get; init; }

    /// <summary>
    /// Default OAuth redirect URI registered for the web application.
    /// </summary>
    public string RedirectUri { get; init; } = string.Empty;

    public string[] Scopes { get; init; } = ["openid", "email", "profile"];

    public bool UseAwsHostedUi => Enabled && !string.IsNullOrWhiteSpace(Domain);

    public bool UseDevHostedUi => Enabled && string.IsNullOrWhiteSpace(Domain);
}
