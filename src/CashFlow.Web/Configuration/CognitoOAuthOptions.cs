namespace CashFlow.Web.Configuration;

public sealed class CognitoOAuthOptions
{
    public const string SectionName = "Cognito:OAuth";

    public bool Enabled { get; init; }

    public string Domain { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;

    public string[] Scopes { get; init; } = ["openid", "email", "profile"];
}
