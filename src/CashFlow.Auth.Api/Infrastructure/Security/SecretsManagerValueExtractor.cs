using Amazon.SecretsManager.Model;

namespace CashFlow.Auth.Infrastructure.Security;

internal static class SecretsManagerValueExtractor
{
    public static string? ExtractSecretValue(GetSecretValueResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.SecretString))
        {
            return response.SecretString;
        }

        if (response.SecretBinary is null || response.SecretBinary.Length == 0)
        {
            return null;
        }

        return Convert.ToBase64String(response.SecretBinary.ToArray());
    }
}
