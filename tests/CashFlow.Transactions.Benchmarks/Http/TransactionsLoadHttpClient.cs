namespace CashFlow.Transactions.Benchmarks.Http;

internal sealed class TransactionsLoadHttpClient : IAsyncDisposable
{
    private TransactionsLoadTestFactory? factory;

    private TransactionsLoadHttpClient(HttpClient client, TransactionsLoadTestFactory? factory)
    {
        Client = client;
        this.factory = factory;
    }

    public HttpClient Client { get; }

    public static TransactionsLoadHttpClient Create(string? baseUrl, string bearerToken)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(NormalizeBaseUrl(baseUrl)),
                Timeout = TimeSpan.FromSeconds(30)
            };

            ApplyAuthorization(client, bearerToken);
            return new TransactionsLoadHttpClient(client, factory: null);
        }

        var testFactory = new TransactionsLoadTestFactory();
        var inMemoryClient = testFactory.CreateClient();
        ApplyAuthorization(inMemoryClient, bearerToken);
        return new TransactionsLoadHttpClient(inMemoryClient, testFactory);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        if (factory is not null)
        {
            await factory.DisposeAsync();
            factory = null;
        }
    }

    private static void ApplyAuthorization(HttpClient client, string bearerToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        return normalized.EndsWith('/') ? normalized : normalized + "/";
    }
}
