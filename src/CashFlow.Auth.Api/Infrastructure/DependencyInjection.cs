using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.KeyManagementService;
using Amazon.SecretsManager;
using Aspire.CashFlow.ServiceDefaults.Authentication;
using Aspire.CashFlow.ServiceDefaults.Aws;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Identity;
using CashFlow.Auth.Infrastructure.Identity.Abstractions;
using CashFlow.Auth.Infrastructure.OAuth.Abstractions;
using CashFlow.Auth.Infrastructure.Persistence.Abstractions;
using CashFlow.Auth.Infrastructure.Security.Abstractions;
using CashFlow.Auth.Infrastructure.OAuth;
using CashFlow.Auth.Infrastructure.Observability;
using CashFlow.Auth.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<CognitoOAuthOptions>(
            configuration.GetSection(CognitoOAuthOptions.SectionName)
        );
        services.Configure<LocalAuthOptions>(
            configuration.GetSection(LocalAuthOptions.SectionName)
        );
        services.Configure<SecretsManagerOptions>(
            configuration.GetSection(SecretsManagerOptions.SectionName)
        );
        services.Configure<KmsOptions>(configuration.GetSection(KmsOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IAmazonSecretsManager>(serviceProvider =>
        {
            var secretsManagerOptions = serviceProvider
                .GetRequiredService<IOptions<SecretsManagerOptions>>()
                .Value;
            var awsOptions = serviceProvider.GetRequiredService<IOptions<AwsOptions>>().Value;
            return AmazonSecretsManagerClientFactory.Create(secretsManagerOptions, awsOptions);
        });
        services.AddSingleton<ISecretsManagerGateway>(
            serviceProvider => new AwsSecretsManagerGateway(
                serviceProvider.GetRequiredService<IAmazonSecretsManager>()
            )
        );
        services.AddSingleton<IAmazonKeyManagementService>(serviceProvider =>
        {
            var kmsOptions = serviceProvider.GetRequiredService<IOptions<KmsOptions>>().Value;
            var awsOptions = serviceProvider.GetRequiredService<IOptions<AwsOptions>>().Value;
            return AmazonKmsClientFactory.Create(kmsOptions, awsOptions);
        });
        services.AddSingleton<IKmsGateway>(serviceProvider => new AwsKmsGateway(
            serviceProvider.GetRequiredService<IAmazonKeyManagementService>()
        ));
        services.AddSingleton<KmsEncryptionService>();
        services.AddSingleton<IEncryptionPolicyService>(serviceProvider =>
            serviceProvider.GetRequiredService<KmsEncryptionService>()
        );
        services.AddSingleton<IKmsEncryptionService>(serviceProvider =>
            serviceProvider.GetRequiredService<KmsEncryptionService>()
        );
        services.AddSingleton<IAmazonCognitoIdentityProvider>(serviceProvider =>
        {
            var cognitoOptions = serviceProvider
                .GetRequiredService<IOptions<CognitoOptions>>()
                .Value;
            var awsOptions = serviceProvider.GetRequiredService<IOptions<AwsOptions>>().Value;
            var regionName = AwsCredentialResolver.ResolveRegion(awsOptions, cognitoOptions.Region);
            var region = RegionEndpoint.GetBySystemName(regionName);

            if (cognitoOptions.UseLocalStack)
            {
                var config = new AmazonCognitoIdentityProviderConfig
                {
                    ServiceURL = cognitoOptions.ServiceUrl,
                    AuthenticationRegion = regionName,
                    UseHttp = cognitoOptions.ServiceUrl!.StartsWith(
                        "http://",
                        StringComparison.OrdinalIgnoreCase
                    ),
                };

                return new AmazonCognitoIdentityProviderClient(
                    AwsCredentialResolver.Resolve(awsOptions),
                    config
                );
            }

            return new AmazonCognitoIdentityProviderClient(
                AwsCredentialResolver.Resolve(awsOptions),
                region
            );
        });
        services.AddHttpClient(nameof(CognitoOAuthService));
        services.AddSingleton<DevAuthorizationCodeStore>();
        services.AddScoped<ICognitoOAuthService, CognitoOAuthService>();
        services.AddScoped<ICognitoIdentityGateway, AwsCognitoIdentityGateway>();
        services.AddSingleton<IPasswordVerifier, PasswordVerifier>();
        services.AddSingleton<LocalMfaChallengeStore>();
        services.AddSingleton<InMemoryUserAccountStore>();
        services.AddSingleton<IUserAccountStore>(serviceProvider =>
            serviceProvider.GetRequiredService<InMemoryUserAccountStore>()
        );
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IIdentityProvider, CognitoIdentityProvider>();
        services.AddScoped<CognitoUserAdministrationService>();
        services.AddScoped<InMemoryUserAdministrationService>();
        services.AddScoped<IUserAdministrationService>(serviceProvider =>
        {
            var cognitoOptions = serviceProvider
                .GetRequiredService<IOptions<CognitoOptions>>()
                .Value;
            return cognitoOptions.IsConfigured
                ? serviceProvider.GetRequiredService<CognitoUserAdministrationService>()
                : serviceProvider.GetRequiredService<InMemoryUserAdministrationService>();
        });
        services.AddSingleton<ISecretProvider, SecretsManagerSecretProvider>();
        services.AddSingleton<AuthMetrics>();
        services.AddSingleton<AuthLogEvents>();
        services.AddSingleton<SecurityAuditService>();

        return services;
    }
}
