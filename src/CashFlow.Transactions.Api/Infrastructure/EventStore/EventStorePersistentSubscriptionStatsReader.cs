using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CashFlow.Transactions.Infrastructure.EventStore;

public sealed class EventStorePersistentSubscriptionStatsReader(
    IHttpClientFactory httpClientFactory,
    ILogger<EventStorePersistentSubscriptionStatsReader> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PersistentSubscriptionStats?> TryGetToAllStatsAsync(
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient("eventstore");
        var requestUri = $"subscriptions/%24all/{Uri.EscapeDataString(groupName)}/info";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogDebug(
                "EventStore subscription stats unavailable for {GroupName} ({StatusCode}).",
                groupName,
                (int)response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return TryParseStats(json, groupName);
    }

    internal static PersistentSubscriptionStats? TryParseStats(string json, string groupName)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in root.EnumerateArray())
            {
                if (TryGetString(entry, "groupName") is { } name
                    && string.Equals(name, groupName, StringComparison.Ordinal)
                    && TryReadStats(entry, out var arrayStats))
                {
                    return arrayStats;
                }
            }

            return null;
        }

        return TryReadStats(root, out var stats) ? stats : null;
    }

    private static bool TryReadStats(JsonElement element, out PersistentSubscriptionStats stats)
    {
        stats = default!;
        if (!TryGetLong(element, "lastKnownEventNumber", out var lastKnown)
            || !TryGetLong(element, "lastProcessedEventNumber", out var lastProcessed))
        {
            return false;
        }

        _ = TryGetLong(element, "totalInFlightMessages", out var inFlight);
        _ = TryGetLong(element, "parkedMessageCount", out var parked);

        stats = new PersistentSubscriptionStats(
            Math.Max(0, lastKnown - lastProcessed),
            inFlight,
            parked);

        return true;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : null;
        }

        return null;
    }

    private static bool TryGetLong(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out value))
            {
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && long.TryParse(property.Value.GetString(), out value))
            {
                return true;
            }
        }

        return false;
    }
}

public readonly record struct PersistentSubscriptionStats(
    long LagEvents,
    long InFlightMessages,
    long ParkedMessages);
