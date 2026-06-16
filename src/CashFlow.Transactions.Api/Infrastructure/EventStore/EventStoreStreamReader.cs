using System.Text.Json;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.EventStore.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Infrastructure.EventStore;

public sealed class EventStoreStreamReader(
    HttpClient httpClient,
    IOptions<EventStoreOptions> eventStoreOptions,
    ILogger<EventStoreStreamReader> logger
) : IEventStoreTransactionReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TransactionRecordedEvent?> TryGetByEventIdAsync(
        string userId,
        Guid eventId,
        CancellationToken cancellationToken = default
    )
    {
        var streamName = BuildStreamName(userId);
        var position = 0;
        var settings = eventStoreOptions.Value;

        for (var page = 0; page < settings.MaxReadPages; page++)
        {
            var requestUri =
                $"streams/{Uri.EscapeDataString(streamName)}/{position}/backward/{settings.BackwardPageSize}";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.TryAddWithoutValidation(
                "Accept",
                "application/vnd.eventstore.atom+json"
            );

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogDebug(
                    "EventStore stream read failed for {StreamName} with {StatusCode}: {Body}",
                    streamName,
                    (int)response.StatusCode,
                    body
                );
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!TryFindEvent(json, eventId, out var transactionEvent))
            {
                if (!TryGetNextBackwardPosition(json, ref position))
                {
                    return null;
                }

                continue;
            }

            return transactionEvent;
        }

        return null;
    }

    internal static string BuildStreamName(string userId) => $"cashflow-{userId.Trim()}";

    private static bool TryFindEvent(
        string json,
        Guid eventId,
        out TransactionRecordedEvent? transactionEvent
    )
    {
        transactionEvent = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return TryFindInArray(document.RootElement, eventId, out transactionEvent);
            }

            if (
                document.RootElement.TryGetProperty("entries", out var entries)
                && entries.ValueKind == JsonValueKind.Array
            )
            {
                return TryFindInArray(entries, eventId, out transactionEvent);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryFindInArray(
        JsonElement entries,
        Guid eventId,
        out TransactionRecordedEvent? transactionEvent
    )
    {
        transactionEvent = null;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!TryParseEntry(entry, out var entryEventId, out var eventType, out var data))
            {
                continue;
            }

            if (entryEventId != eventId)
            {
                continue;
            }

            if (TransactionRecordedEventParser.TryParse(data, eventType, out transactionEvent))
            {
                return transactionEvent is not null;
            }
        }

        return false;
    }

    private static bool TryParseEntry(
        JsonElement entry,
        out Guid entryEventId,
        out string eventType,
        out ReadOnlyMemory<byte> data
    )
    {
        entryEventId = Guid.Empty;
        eventType = string.Empty;
        data = ReadOnlyMemory<byte>.Empty;

        if (!entry.TryGetProperty("eventId", out var eventIdElement))
        {
            return false;
        }

        if (!Guid.TryParse(eventIdElement.GetString(), out entryEventId))
        {
            return false;
        }

        if (!entry.TryGetProperty("eventType", out var eventTypeElement))
        {
            return false;
        }

        eventType = eventTypeElement.GetString() ?? string.Empty;
        if (!entry.TryGetProperty("data", out var dataElement))
        {
            return false;
        }

        data = dataElement.ValueKind switch
        {
            JsonValueKind.String => System.Text.Encoding.UTF8.GetBytes(
                dataElement.GetString() ?? string.Empty
            ),
            _ => ReadOnlyMemory<byte>.Empty,
        };

        return data.Length > 0;
    }

    private static bool TryGetNextBackwardPosition(string json, ref int position)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var entries =
                root.ValueKind == JsonValueKind.Array ? root
                : root.TryGetProperty("entries", out var nested) ? nested
                : default;

            if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
            {
                return false;
            }

            var last = entries[entries.GetArrayLength() - 1];
            if (!last.TryGetProperty("eventNumber", out var eventNumberElement))
            {
                return false;
            }

            var eventNumber = eventNumberElement.GetInt32();
            if (eventNumber <= 0)
            {
                return false;
            }

            position = eventNumber - 1;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
