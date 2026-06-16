using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

namespace CashFlow.Auth.Infrastructure.Security;

internal sealed class AwsKmsGateway(IAmazonKeyManagementService kmsClient) : IKmsGateway
{
    public async Task<byte[]?> EncryptAsync(string keyId, byte[] plaintext, CancellationToken cancellationToken = default)
    {
        if (plaintext.Length == 0)
        {
            return Array.Empty<byte>();
        }

        try
        {
            var response = await kmsClient.EncryptAsync(new EncryptRequest
            {
                KeyId = keyId,
                Plaintext = new MemoryStream(plaintext)
            }, cancellationToken);

            return response.CiphertextBlob?.ToArray();
        }
        catch (NotFoundException)
        {
            return null;
        }
        catch (KMSInvalidStateException)
        {
            return null;
        }
    }

    public async Task<byte[]?> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        if (ciphertext.Length == 0)
        {
            return Array.Empty<byte>();
        }

        try
        {
            var response = await kmsClient.DecryptAsync(new DecryptRequest
            {
                CiphertextBlob = new MemoryStream(ciphertext)
            }, cancellationToken);

            return response.Plaintext?.ToArray();
        }
        catch (InvalidCiphertextException)
        {
            return null;
        }
        catch (KMSInvalidStateException)
        {
            return null;
        }
    }
}
