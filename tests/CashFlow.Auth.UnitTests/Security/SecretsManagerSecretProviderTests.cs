using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text;

namespace CashFlow.Auth.UnitTests.Security;

public sealed class SecretsManagerSecretProviderTests
{
    [Fact]
    public async Task GetSecretAsync_ReturnsAwsValue_WhenSecretExists()
    {
        var provider = CreateProvider(
            new FakeSecretsManagerGateway
            {
                Secrets = new Dictionary<string, string>
                {
                    ["cashflow/Auth/JwtSigningKey"] = "aws-signing-key"
                }
            },
            new ConfigurationBuilder().Build(),
            new SecretsManagerOptions { Prefix = "cashflow/", PreferConfiguration = false });

        var value = await provider.GetSecretAsync("Auth/JwtSigningKey");
        Assert.Equal("aws-signing-key", value);
    }

    [Fact]
    public async Task GetSecretAsync_FallsBackToConfiguration_WhenAwsSecretIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:Auth/JwtSigningKey"] = "configured-signing-key"
            })
            .Build();

        var provider = CreateProvider(
            new FakeSecretsManagerGateway(),
            configuration,
            new SecretsManagerOptions { Prefix = "cashflow/", PreferConfiguration = false });

        var value = await provider.GetSecretAsync("Auth/JwtSigningKey");
        Assert.Equal("configured-signing-key", value);
    }

    [Fact]
    public async Task GetSecretAsync_PrefersConfiguration_WhenPreferConfigurationIsEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:Auth/JwtSigningKey"] = "configured-signing-key"
            })
            .Build();

        var provider = CreateProvider(
            new FakeSecretsManagerGateway
            {
                Secrets = new Dictionary<string, string>
                {
                    ["cashflow/Auth/JwtSigningKey"] = "aws-signing-key"
                }
            },
            configuration,
            new SecretsManagerOptions { Prefix = "cashflow/", PreferConfiguration = true });

        var value = await provider.GetSecretAsync("Auth/JwtSigningKey");
        Assert.Equal("configured-signing-key", value);
    }

    [Fact]
    public void RuntimeSecretLoader_LoadsJwtSigningKey_FromAws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuntimeSecrets:Jwt:SigningKey"] = "Auth/JwtSigningKey"
            })
            .Build();

        var gateway = new FakeSecretsManagerGateway
        {
            Secrets = new Dictionary<string, string>
            {
                ["cashflow/Auth/JwtSigningKey"] = "runtime-signing-key"
            }
        };

        var overrides = RuntimeSecretLoader.LoadConfigurationOverrides(
            configuration,
            new SecretsManagerOptions { Prefix = "cashflow/", PreferConfiguration = false },
            gateway);

        Assert.Equal("runtime-signing-key", overrides["Jwt:SigningKey"]);
    }

    [Fact]
    public async Task GetSecretAsync_DecryptsKmsProtectedValues()
    {
        var kms = new FakeKmsEncryptionService();
        var encrypted = $"kms:{await kms.EncryptToBase64Async("protected-signing-key", "Secrets")}";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:Auth/JwtSigningKey"] = encrypted
            })
            .Build();

        var provider = CreateProvider(
            new FakeSecretsManagerGateway(),
            kms,
            configuration,
            new SecretsManagerOptions { Prefix = "cashflow/", PreferConfiguration = true });

        var value = await provider.GetSecretAsync("Auth/JwtSigningKey");
        Assert.Equal("protected-signing-key", value);
    }

    private static SecretsManagerSecretProvider CreateProvider(
        ISecretsManagerGateway secretsManagerGateway,
        IKmsEncryptionService kmsEncryptionService,
        IConfiguration configuration,
        SecretsManagerOptions secretsManagerOptions)
    {
        return new SecretsManagerSecretProvider(
            secretsManagerGateway,
            kmsEncryptionService,
            configuration,
            Options.Create(secretsManagerOptions),
            new MemoryCache(new MemoryCacheOptions()));
    }

    private sealed class FakeKmsEncryptionService : IKmsEncryptionService
    {
        public Task<byte[]> EncryptAsync(byte[] plaintext, string purpose, CancellationToken cancellationToken = default)
            => Task.FromResult(Encoding.UTF8.GetBytes($"enc:{purpose}:{Convert.ToBase64String(plaintext)}"));

        public Task<byte[]?> DecryptAsync(byte[] ciphertext, string purpose, CancellationToken cancellationToken = default)
        {
            var encoded = Encoding.UTF8.GetString(ciphertext);
            var parts = encoded.Split(':', 3);
            return Task.FromResult<byte[]?>(Convert.FromBase64String(parts[2]));
        }

        public Task<string> EncryptToBase64Async(string plaintext, string purpose, CancellationToken cancellationToken = default)
        {
            var payload = $"enc:{purpose}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))}";
            return Task.FromResult(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)));
        }

        public Task<string?> DecryptFromBase64Async(string ciphertextBase64, string purpose, CancellationToken cancellationToken = default)
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(ciphertextBase64));
            var parts = decoded.Split(':', 3);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])));
        }
    }

    private static SecretsManagerSecretProvider CreateProvider(
        ISecretsManagerGateway secretsManagerGateway,
        IConfiguration configuration,
        SecretsManagerOptions secretsManagerOptions)
    {
        return CreateProvider(
            secretsManagerGateway,
            new FakeKmsEncryptionService(),
            configuration,
            secretsManagerOptions);
    }

    private sealed class FakeSecretsManagerGateway : ISecretsManagerGateway
    {
        public Dictionary<string, string> Secrets { get; init; } = new(StringComparer.Ordinal);

        public Task<string?> GetSecretStringAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Secrets.TryGetValue(secretId, out var value) ? value : null);
        }
    }
}
