using Aspire.CashFlow.ServiceDefaults.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aspire.CashFlow.ServiceDefaults.Logging;

public static class CloudWatchLoggingExtensions
{
    public static IHostApplicationBuilder AddCloudWatchLogging(this IHostApplicationBuilder builder)
    {
        builder
            .Services.AddOptions<CloudWatchOptions>()
            .Bind(builder.Configuration.GetSection(CloudWatchOptions.SectionName));

        var options =
            builder.Configuration.GetSection(CloudWatchOptions.SectionName).Get<CloudWatchOptions>()
            ?? new CloudWatchOptions();

        if (!options.Enabled)
        {
            return builder;
        }

        builder.Services.TryAddSingleton<CloudWatchLogWriter>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, CloudWatchLoggerProvider>()
        );
        builder.Services.AddHostedService(sp => sp.GetRequiredService<CloudWatchLogWriter>());

        return builder;
    }
}
