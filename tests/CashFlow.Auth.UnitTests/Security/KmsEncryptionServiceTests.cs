using System.Text;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Security;
using CashFlow.Auth.Infrastructure.Security.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.UnitTests.Security;

public sealed class KmsEncryptionServiceTests
{
    [Fact]
    public async Task EncryptAndDecrypt_RoundTrip_ReturnsOriginalPlaintext()
    {
        var gateway = new FakeKmsGateway();
        var service = new KmsEncryptionService(
            gateway,
            Options.Create(new KmsOptions { SecretsKeyId = "alias/cashflow-secrets" })
        );

        var plaintext = "sensitive-value";
        var ciphertextBase64 = await service.EncryptToBase64Async(plaintext, "Secrets");
        var decrypted = await service.DecryptFromBase64Async(ciphertextBase64, "Secrets");

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void GetKeyIdentifier_ReturnsConfiguredKeyForPurpose()
    {
        var service = new KmsEncryptionService(
            new FakeKmsGateway(),
            Options.Create(
                new KmsOptions
                {
                    DefaultKeyId = "alias/cashflow-default",
                    SecretsKeyId = "alias/cashflow-secrets",
                    StorageKeyId = "alias/cashflow-storage",
                }
            )
        );

        Assert.Equal("alias/cashflow-secrets", service.GetKeyIdentifier("Secrets"));
        Assert.Equal("alias/cashflow-storage", service.GetKeyIdentifier("Storage"));
        Assert.Equal("alias/cashflow-default", service.GetKeyIdentifier("Other"));
    }

    [Fact]
    public async Task EncryptAsync_Throws_WhenKmsReturnsNoCiphertext()
    {
        var service = new KmsEncryptionService(
            new FakeKmsGateway { EncryptReturnsNull = true },
            Options.Create(new KmsOptions())
        );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EncryptToBase64Async("value", "Secrets")
        );
    }

    private sealed class FakeKmsGateway : IKmsGateway
    {
        public bool EncryptReturnsNull { get; init; }

        public Task<byte[]?> EncryptAsync(
            string keyId,
            byte[] plaintext,
            CancellationToken cancellationToken = default
        )
        {
            if (EncryptReturnsNull)
            {
                return Task.FromResult<byte[]?>(null);
            }

            var payload = Convert.ToBase64String(plaintext);
            return Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes($"enc:{keyId}:{payload}"));
        }

        public Task<byte[]?> DecryptAsync(
            byte[] ciphertext,
            CancellationToken cancellationToken = default
        )
        {
            var encoded = Encoding.UTF8.GetString(ciphertext);
            if (!encoded.StartsWith("enc:", StringComparison.Ordinal))
            {
                return Task.FromResult<byte[]?>(null);
            }

            var payload = encoded.Split(':', 3)[2];
            return Task.FromResult<byte[]?>(Convert.FromBase64String(payload));
        }
    }
}
