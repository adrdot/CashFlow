using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Aspire.CashFlow.ServiceDefaults.Authentication;

public static class CashFlowAuthenticationExtensions
{
    private static readonly ConcurrentDictionary<
        string,
        IReadOnlyCollection<SecurityKey>
    > LocalStackSigningKeys = new(StringComparer.Ordinal);

    public static IServiceCollection AddCashFlowJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var cognitoSection = configuration.GetSection(CognitoOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
        var cognitoOptions = cognitoSection.Get<CognitoOptions>() ?? new CognitoOptions();

        services.Configure<JwtOptions>(jwtSection);
        services.Configure<CognitoOptions>(cognitoSection);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (cognitoOptions.IsConfigured)
                {
                    ConfigureCognitoJwtBearer(options, cognitoOptions);
                }
                else
                {
                    options.TokenValidationParameters = CreateLocalValidationParameters(jwtOptions);
                }
            });

        services.AddAuthorization();
        return services;
    }

    public static TokenValidationParameters CreateLocalValidationParameters(JwtOptions options)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds),
        };
    }

    private static void ConfigureCognitoJwtBearer(
        JwtBearerOptions options,
        CognitoOptions cognitoOptions
    )
    {
        options.RequireHttpsMetadata = !cognitoOptions.UseLocalStack;

        if (cognitoOptions.UseLocalStack)
        {
            var authority = cognitoOptions.Authority;
            options.Authority = authority;
            options.MetadataAddress = $"{authority}/.well-known/openid-configuration";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authority,
                ValidateLifetime = true,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromMinutes(cognitoOptions.ClockSkewMinutes),
                IssuerSigningKeyResolver = (_, _, keyId, _) =>
                    ResolveLocalStackSigningKeys(cognitoOptions, keyId),
            };
        }
        else
        {
            options.Authority = cognitoOptions.Authority;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = cognitoOptions.Authority,
                ValidateLifetime = true,
                ValidateAudience = false,
                RoleClaimType = "cognito:groups",
            };
        }
    }

    private static IEnumerable<SecurityKey> ResolveLocalStackSigningKeys(
        CognitoOptions cognitoOptions,
        string? keyId
    )
    {
        var keys = LocalStackSigningKeys.GetOrAdd(
            cognitoOptions.Authority,
            _ => LoadSigningKeys(cognitoOptions)
        );
        if (string.IsNullOrWhiteSpace(keyId))
        {
            return keys;
        }

        return keys.Where(key => string.Equals(key.KeyId, keyId, StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<SecurityKey> LoadSigningKeys(CognitoOptions cognitoOptions)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(cognitoOptions.JwksTimeoutSeconds),
        };
        var jwksJson = httpClient.GetStringAsync(cognitoOptions.JwksUri).GetAwaiter().GetResult();
        return new JsonWebKeySet(jwksJson).GetSigningKeys().ToList();
    }
}
