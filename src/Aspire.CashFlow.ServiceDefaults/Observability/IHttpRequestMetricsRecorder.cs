namespace Aspire.CashFlow.ServiceDefaults.Observability;

public interface IHttpRequestMetricsRecorder
{
    void RecordHttpRequest(string method, string route, int statusCode);
}
