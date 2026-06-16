using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace CashFlow.Auth.Infrastructure;

public static class RuntimeSecretConfigurationExtensions
{
    public static WebApplicationBuilder AddCashFlowRuntimeSecrets(this WebApplicationBuilder builder)
    {
        var secretsManagerOptions = AmazonSecretsManagerClientFactory.ResolveOptions(builder.Configuration);
        var overrides = RuntimeSecretLoader.LoadConfigurationOverrides(builder.Configuration, secretsManagerOptions);

        if (overrides.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(overrides);
        }

        return builder;
    }
}
