namespace CashFlow.Auth.Infrastructure.Security;

public interface ISecretsManagerGateway
{
    Task<string?> GetSecretStringAsync(string secretId, CancellationToken cancellationToken = default);
}
