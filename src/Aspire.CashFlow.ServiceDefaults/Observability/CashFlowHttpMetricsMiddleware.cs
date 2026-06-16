using Microsoft.AspNetCore.Http;

namespace Aspire.CashFlow.ServiceDefaults.Observability;

public sealed class CashFlowHttpMetricsMiddleware<TMetrics>(
    RequestDelegate next,
    string pathPrefix
)
    where TMetrics : class, IHttpRequestMetricsRecorder
{
    public async Task InvokeAsync(HttpContext context, TMetrics metrics)
    {
        if (!context.Request.Path.StartsWithSegments(pathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        await next(context);

        metrics.RecordHttpRequest(
            context.Request.Method,
            context.Request.Path.Value ?? pathPrefix,
            context.Response.StatusCode
        );
    }
}
