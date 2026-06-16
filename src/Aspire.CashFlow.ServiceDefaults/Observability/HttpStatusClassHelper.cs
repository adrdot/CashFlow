namespace Aspire.CashFlow.ServiceDefaults.Observability;

public static class HttpStatusClassHelper
{
    public static string ToStatusClass(int statusCode) =>
        statusCode switch
        {
            >= 500 => "5xx",
            >= 400 => "4xx",
            >= 300 => "3xx",
            >= 200 => "2xx",
            _ => "other",
        };
}
