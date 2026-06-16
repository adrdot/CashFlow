using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Aspire.CashFlow.ServiceDefaults.Observability;

public static class CashFlowObservabilityExtensions
{
    public static IServiceCollection AddCashFlowMeter<TMetrics>(
        this IServiceCollection services,
        string meterName
    )
        where TMetrics : class
    {
        services.AddSingleton<TMetrics>();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(meterName));
        return services;
    }

    /// <summary>
    /// Optional custom HTTP counter middleware. Prefer OTEL <c>AddAspNetCoreInstrumentation</c> (see ServiceDefaults).
    /// </summary>
    public static IApplicationBuilder UseCashFlowHttpMetrics<TMetrics>(
        this IApplicationBuilder app,
        string pathPrefix
    )
        where TMetrics : class, IHttpRequestMetricsRecorder
    {
        return app.UseMiddleware<CashFlowHttpMetricsMiddleware<TMetrics>>(pathPrefix);
    }
}
