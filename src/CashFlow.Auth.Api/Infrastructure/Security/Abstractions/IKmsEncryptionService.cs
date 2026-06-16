namespace CashFlow.Auth.Infrastructure.Security.Abstractions;

public interface IKmsEncryptionService
{
    Task<byte[]> EncryptAsync(
        byte[] plaintext,
        string purpose,
        CancellationToken cancellationToken = default
    );

    Task<byte[]?> DecryptAsync(
        byte[] ciphertext,
        string purpose,
        CancellationToken cancellationToken = default
    );

    Task<string> EncryptToBase64Async(
        string plaintext,
        string purpose,
        CancellationToken cancellationToken = default
    );

    Task<string?> DecryptFromBase64Async(
        string ciphertextBase64,
        string purpose,
        CancellationToken cancellationToken = default
    );
}
