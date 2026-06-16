using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace CashFlow.Transactions.Infrastructure.EventStore;

public sealed class EventStoreTransactionWriter(
    HttpClient httpClient,
    TransactionMetrics metrics,
    ILogger<EventStoreTransactionWriter> logger) : IEventStoreTransactionWriter
{
    private const string ExpectedVersionAny = "-2";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task AppendAsync(
        TransactionRecordedEvent transactionEvent,
        Guid eventId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var succeeded = false;

        try
        {
            var streamName = EventStoreStreamReader.BuildStreamName(transactionEvent.UserId);
            var payload = JsonSerializer.Serialize(new[]
            {
                new
                {
                    eventId,
                    eventType = "TransactionRecorded",
                    data = transactionEvent,
                    metadata = new
                    {
                        schemaVersion = 1,
                        transactionEvent.TransactionId,
                        transactionEvent.CreatedAtUtc,
                        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
                    }
                }
            }, JsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"streams/{Uri.EscapeDataString(streamName)}")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/vnd.eventstore.events+json")
            };
            request.Headers.Add("ES-ExpectedVersion", ExpectedVersionAny);
            request.Headers.TryAddWithoutValidation("Kurrent-ExpectedVersion", ExpectedVersionAny);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"EventStore append failed with {(int)response.StatusCode}: {body}");
            }

            succeeded = true;
            logger.LogInformation(
                "Appended transaction {TransactionId} to stream {StreamName} with event id {EventId}.",
                transactionEvent.TransactionId,
                streamName,
                eventId);
        }
        finally
        {
            metrics.RecordEventStoreAppendDuration(
                stopwatch.Elapsed,
                succeeded ? "success" : "failure");
        }
    }
}

public static class EventStoreHttpClientFactory
{
    public static HttpClient Create(EventStoreOptions options)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(options.HttpEndpoint.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
