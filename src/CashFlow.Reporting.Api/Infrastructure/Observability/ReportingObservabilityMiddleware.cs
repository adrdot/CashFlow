namespace CashFlow.Reporting.Infrastructure.Observability;

public sealed class ReportingObservabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ReportingMetrics metrics)
    {
        if (!context.Request.Path.StartsWithSegments("/api/reports", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        await next(context);

        metrics.RecordHttpRequest(
            context.Request.Method,
            context.Request.Path.Value ?? "/api/reports",
            context.Response.StatusCode);
    }
}
