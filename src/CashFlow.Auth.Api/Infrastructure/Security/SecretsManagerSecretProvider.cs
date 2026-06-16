using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.Infrastructure.Security;

public sealed class SecretsManagerSecretProvider(
    ISecretsManagerGateway secretsManagerGateway,
    IKmsEncryptionService kmsEncryptionService,
    IConfiguration configuration,
    IOptions<SecretsManagerOptions> options,
    IMemoryCache memoryCache) : ISecretProvider
{
    private const string KmsProtectedPrefix = "kms:";

    private readonly SecretsManagerOptions secretsOptions = options.Value;

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return null;
        }

        var cacheKey = $"secrets-manager::{secretsOptions.Prefix}::{secretName}";
        if (secretsOptions.EnableCaching
            && memoryCache.TryGetValue(cacheKey, out string? cachedValue))
        {
            return await ResolveProtectedValueAsync(cachedValue, cancellationToken);
        }

        string? resolvedValue = null;

        if (secretsOptions.PreferConfiguration)
        {
            resolvedValue = ReadFromConfiguration(secretName);
        }

        if (string.IsNullOrWhiteSpace(resolvedValue))
        {
            var secretId = BuildSecretId(secretName);
            resolvedValue = await secretsManagerGateway.GetSecretStringAsync(secretId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(resolvedValue))
        {
            resolvedValue = ReadFromConfiguration(secretName);
        }

        if (!string.IsNullOrWhiteSpace(resolvedValue))
        {
            CacheSecret(cacheKey, resolvedValue);
        }

        return await ResolveProtectedValueAsync(resolvedValue, cancellationToken);
    }

    private async Task<string?> ResolveProtectedValueAsync(string? secretValue, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secretValue)
            || !secretValue.StartsWith(KmsProtectedPrefix, StringComparison.Ordinal))
        {
            return secretValue;
        }

        return await kmsEncryptionService.DecryptFromBase64Async(
            secretValue[KmsProtectedPrefix.Length..],
            "Secrets",
            cancellationToken);
    }

    private string BuildSecretId(string secretName)
    {
        var normalizedName = secretName.Replace(':', '/').Trim('/');
        var prefix = secretsOptions.Prefix.TrimEnd('/');
        return string.IsNullOrWhiteSpace(prefix) ? normalizedName : $"{prefix}/{normalizedName}";
    }

    private string? ReadFromConfiguration(string secretName)
    {
        var normalizedName = secretName.Replace(':', '/');
        var fullKey = $"{secretsOptions.Prefix.TrimEnd('/')}/{normalizedName}";
        return configuration[$"Secrets:{secretName}"] ?? configuration[fullKey];
    }

    private void CacheSecret(string cacheKey, string secretValue)
    {
        if (!secretsOptions.EnableCaching)
        {
            return;
        }

        memoryCache.Set(
            cacheKey,
            secretValue,
            TimeSpan.FromMinutes(Math.Max(1, secretsOptions.CacheDurationMinutes)));
    }
}
