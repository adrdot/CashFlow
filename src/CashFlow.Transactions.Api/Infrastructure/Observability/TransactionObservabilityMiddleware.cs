namespace CashFlow.Transactions.Infrastructure.Observability;

public sealed class TransactionObservabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TransactionMetrics metrics)
    {
        if (!context.Request.Path.StartsWithSegments("/api/transactions", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        await next(context);

        metrics.RecordHttpRequest(
            context.Request.Method,
            context.Request.Path.Value ?? "/api/transactions",
            context.Response.StatusCode);
    }
}
