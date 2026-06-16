using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using CashFlow.Auth.Infrastructure.Security.Abstractions;

namespace CashFlow.Auth.Infrastructure.Security;

internal sealed class AwsSecretsManagerGateway(IAmazonSecretsManager secretsManagerClient)
    : ISecretsManagerGateway
{
    public async Task<string?> GetSecretStringAsync(
        string secretId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await secretsManagerClient.GetSecretValueAsync(
                new GetSecretValueRequest { SecretId = secretId },
                cancellationToken
            );

            return SecretsManagerValueExtractor.ExtractSecretValue(response);
        }
        catch (ResourceNotFoundException)
        {
            return null;
        }
        catch (DecryptionFailureException)
        {
            return null;
        }
    }
}
