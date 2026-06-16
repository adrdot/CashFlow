namespace CashFlow.Auth.Infrastructure.Security.Abstractions;

public interface ISecretsManagerGateway
{
    Task<string?> GetSecretStringAsync(
        string secretId,
        CancellationToken cancellationToken = default
    );
}
