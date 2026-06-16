namespace CashFlow.Auth.Application.Abstractions;

public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
}