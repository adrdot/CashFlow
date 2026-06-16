namespace CashFlow.Auth.Infrastructure.Security.Abstractions;

public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}
