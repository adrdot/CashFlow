namespace CashFlow.Reporting.Benchmarks.Http;

internal sealed class ReportingLoadHttpClient : IAsyncDisposable
{
    public HttpClient Client { get; }

    private ReportingLoadHttpClient(HttpClient client) => Client = client;

    public static ReportingLoadHttpClient Create(string baseUrl, string? bearerToken)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Port <= 0)
        {
            throw new InvalidOperationException(
                $"Reporting load test base URL is invalid: '{baseUrl}'. " +
                "Use --url with a real endpoint, e.g. https://localhost:7090, " +
                "or set CASHFLOW_REPORTING_URL from the Aspire dashboard (reporting-api).");
        }

        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 128,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
        }

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(uri.ToString().TrimEnd('/') + "/")
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return new ReportingLoadHttpClient(client);
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}
