namespace AspireApp1.ServiceDefaults.Authentication;

public sealed class CognitoOptions
{
    public const string SectionName = "Cognito";

    public bool Enabled { get; init; }

    public string UserPoolId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string Region { get; init; } = "us-east-1";

    public string? ServiceUrl { get; init; }

    public bool UseLocalStack => !string.IsNullOrWhiteSpace(ServiceUrl);

    public bool IsConfigured => Enabled
        && !string.IsNullOrWhiteSpace(UserPoolId)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(Region);

    public string Authority => UseLocalStack
        ? $"{ServiceUrl!.TrimEnd('/')}/{UserPoolId}"
        : $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}";

    public Uri JwksUri => new($"{Authority}/.well-known/jwks.json");
}
