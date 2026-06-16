using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aspire.CashFlow.ServiceDefaults.Security;

public static class CashFlowSecurityExtensions
{
    public const string DefaultCorsPolicy = "CashFlowTrustedOrigins";
    public const string AuthLoginRateLimitPolicy = "auth-login";

    public static IServiceCollection AddCashFlowSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        var securityOptions =
            configuration
                .GetSection(CashFlowSecurityOptions.SectionName)
                .Get<CashFlowSecurityOptions>() ?? new CashFlowSecurityOptions();

        services.Configure<CashFlowSecurityOptions>(
            configuration.GetSection(CashFlowSecurityOptions.SectionName)
        );

        services.AddCors(options =>
        {
            options.AddPolicy(
                DefaultCorsPolicy,
                policy =>
                {
                    if (environment.IsDevelopment())
                    {
                        policy
                            .SetIsOriginAllowed(origin =>
                                Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                                && (
                                    string.Equals(
                                        uri.Host,
                                        "localhost",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                    || string.Equals(
                                        uri.Host,
                                        "127.0.0.1",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                            )
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                        return;
                    }

                    if (securityOptions.AllowedOrigins.Length > 0)
                    {
                        policy
                            .WithOrigins(securityOptions.AllowedOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    }
                }
            );
        });

        if (securityOptions.RateLimitingEnabled)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: context.Connection.RemoteIpAddress?.ToString()
                                ?? "unknown",
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = securityOptions.GlobalPermitLimit,
                                Window = TimeSpan.FromSeconds(securityOptions.GlobalWindowSeconds),
                                QueueLimit = 0,
                            }
                        )
                );

                options.AddPolicy(
                    AuthLoginRateLimitPolicy,
                    context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: context.Connection.RemoteIpAddress?.ToString()
                                ?? "unknown",
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = securityOptions.AuthLoginPermitLimit,
                                Window = TimeSpan.FromSeconds(
                                    securityOptions.AuthLoginWindowSeconds
                                ),
                                QueueLimit = 0,
                            }
                        )
                );
            });
        }

        return services;
    }

    public static WebApplication UseCashFlowSecurity(this WebApplication app)
    {
        app.UseDefaultSecurityHeaders();
        app.UseCors(DefaultCorsPolicy);

        var securityOptions = app
            .Configuration.GetSection(CashFlowSecurityOptions.SectionName)
            .Get<CashFlowSecurityOptions>();

        if (securityOptions?.RateLimitingEnabled == true)
        {
            app.UseRateLimiter();
        }

        return app;
    }
}
