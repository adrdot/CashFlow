using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Text;

namespace CashFlow.Auth.Infrastructure.Security;

public sealed class KmsEncryptionService(
    IKmsGateway kmsGateway,
    IOptions<KmsOptions> options) : IEncryptionPolicyService, IKmsEncryptionService
{
    private readonly KmsOptions kmsOptions = options.Value;

    public string GetKeyIdentifier(string purpose)
    {
        return purpose switch
        {
            "Secrets" => kmsOptions.SecretsKeyId,
            "Storage" => kmsOptions.StorageKeyId,
            _ => kmsOptions.DefaultKeyId
        };
    }

    public bool RequiresCustomerManagedKey(string resourceType)
    {
        return resourceType.Equals("Secrets", StringComparison.OrdinalIgnoreCase)
            || resourceType.Equals("S3", StringComparison.OrdinalIgnoreCase)
            || resourceType.Equals("Database", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<byte[]> EncryptAsync(byte[] plaintext, string purpose, CancellationToken cancellationToken = default)
    {
        if (plaintext.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var keyId = GetKeyIdentifier(purpose);
        var ciphertext = await kmsGateway.EncryptAsync(keyId, plaintext, cancellationToken);
        if (ciphertext is null || ciphertext.Length == 0)
        {
            throw new InvalidOperationException($"KMS encryption failed for purpose '{purpose}' using key '{keyId}'.");
        }

        return ciphertext;
    }

    public async Task<byte[]?> DecryptAsync(byte[] ciphertext, string purpose, CancellationToken cancellationToken = default)
    {
        if (ciphertext.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var plaintext = await kmsGateway.DecryptAsync(ciphertext, cancellationToken);
        if (plaintext is null)
        {
            throw new InvalidOperationException($"KMS decryption failed for purpose '{purpose}'.");
        }

        return plaintext;
    }

    public async Task<string> EncryptToBase64Async(string plaintext, string purpose, CancellationToken cancellationToken = default)
    {
        var ciphertext = await EncryptAsync(Encoding.UTF8.GetBytes(plaintext), purpose, cancellationToken);
        return Convert.ToBase64String(ciphertext);
    }

    public async Task<string?> DecryptFromBase64Async(string ciphertextBase64, string purpose, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ciphertextBase64))
        {
            return null;
        }

        var plaintext = await DecryptAsync(Convert.FromBase64String(ciphertextBase64), purpose, cancellationToken);
        return plaintext is null ? null : Encoding.UTF8.GetString(plaintext);
    }
}
