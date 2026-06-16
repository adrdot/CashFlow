using Amazon.SecretsManager;
using Aspire.CashFlow.ServiceDefaults.Aws;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Security.Abstractions;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.Infrastructure.Security;

public static class RuntimeSecretLoader
{
    public const string SectionName = "RuntimeSecrets";

    public static Dictionary<string, string?> LoadConfigurationOverrides(
        IConfiguration configuration,
        SecretsManagerOptions secretsManagerOptions,
        AwsOptions awsOptions,
        ISecretsManagerGateway? secretsManagerGateway = null
    )
    {
        var mappings = ReadMappings(configuration);
        var pendingMappings = mappings
            .Where(mapping =>
                !string.IsNullOrWhiteSpace(mapping.Key)
                && !string.IsNullOrWhiteSpace(mapping.Value)
                && string.IsNullOrWhiteSpace(configuration[mapping.Key])
            )
            .ToList();

        if (pendingMappings.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        IAmazonSecretsManager? ownedClient = null;
        if (secretsManagerGateway is null)
        {
            ownedClient = AmazonSecretsManagerClientFactory.Create(
                secretsManagerOptions,
                awsOptions
            );
            secretsManagerGateway = new AwsSecretsManagerGateway(ownedClient);
        }

        try
        {
            var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var (configurationKey, secretName) in pendingMappings)
            {
                var secretValue = TryGetSecretValue(
                    secretsManagerGateway,
                    secretsManagerOptions,
                    secretName,
                    configuration
                );

                if (!string.IsNullOrWhiteSpace(secretValue))
                {
                    overrides[configurationKey] = secretValue;
                }
            }

            return overrides;
        }
        finally
        {
            ownedClient?.Dispose();
        }
    }

    private static Dictionary<string, string> ReadMappings(IConfiguration configuration)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenSection(configuration.GetSection(SectionName), string.Empty, mappings);

        var signingKeySecretName = configuration["Jwt:SigningKeySecretName"];
        if (!string.IsNullOrWhiteSpace(signingKeySecretName))
        {
            mappings.TryAdd("Jwt:SigningKey", signingKeySecretName);
        }

        return mappings;
    }

    private static void FlattenSection(
        IConfigurationSection section,
        string prefix,
        Dictionary<string, string> mappings
    )
    {
        if (!string.IsNullOrWhiteSpace(section.Value))
        {
            mappings[prefix] = section.Value!;
            return;
        }

        foreach (var child in section.GetChildren())
        {
            var childKey = string.IsNullOrEmpty(prefix) ? child.Key : $"{prefix}:{child.Key}";
            FlattenSection(child, childKey, mappings);
        }
    }

    private static string? TryGetSecretValue(
        ISecretsManagerGateway secretsManagerGateway,
        SecretsManagerOptions secretsManagerOptions,
        string secretName,
        IConfiguration configuration
    )
    {
        if (secretsManagerOptions.PreferConfiguration)
        {
            var configuredValue = ReadConfiguredSecret(
                configuration,
                secretsManagerOptions,
                secretName
            );
            if (!string.IsNullOrWhiteSpace(configuredValue))
            {
                return configuredValue;
            }
        }

        var secretId = BuildSecretId(secretsManagerOptions, secretName);
        var secretValue = secretsManagerGateway
            .GetSecretStringAsync(secretId)
            .GetAwaiter()
            .GetResult();
        if (!string.IsNullOrWhiteSpace(secretValue))
        {
            return secretValue;
        }

        return ReadConfiguredSecret(configuration, secretsManagerOptions, secretName);
    }

    private static string? ReadConfiguredSecret(
        IConfiguration configuration,
        SecretsManagerOptions secretsManagerOptions,
        string secretName
    )
    {
        var normalizedName = secretName.Replace(':', '/');
        var fullKey = $"{secretsManagerOptions.Prefix.TrimEnd('/')}/{normalizedName}";
        return configuration[$"Secrets:{secretName}"] ?? configuration[fullKey];
    }

    private static string BuildSecretId(
        SecretsManagerOptions secretsManagerOptions,
        string secretName
    )
    {
        var normalizedName = secretName.Replace(':', '/').Trim('/');
        var prefix = secretsManagerOptions.Prefix.TrimEnd('/');
        return string.IsNullOrWhiteSpace(prefix) ? normalizedName : $"{prefix}/{normalizedName}";
    }
}
