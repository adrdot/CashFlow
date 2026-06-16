namespace CashFlow.Auth.Infrastructure.Security.Abstractions;

public interface IKmsGateway
{
    Task<byte[]?> EncryptAsync(
        string keyId,
        byte[] plaintext,
        CancellationToken cancellationToken = default
    );

    Task<byte[]?> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default);
}
