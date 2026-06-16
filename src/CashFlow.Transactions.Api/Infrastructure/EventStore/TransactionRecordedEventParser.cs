using System.Text;
using System.Text.Json;
using CashFlow.Transactions.Application.Contracts;

namespace CashFlow.Transactions.Infrastructure.EventStore;

internal static class TransactionRecordedEventParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(ReadOnlyMemory<byte> data, string eventType, out TransactionRecordedEvent? transactionEvent)
    {
        transactionEvent = null;

        if (!string.Equals(eventType, "TransactionRecorded", StringComparison.Ordinal))
        {
            return false;
        }

        if (data.IsEmpty)
        {
            return false;
        }

        try
        {
            transactionEvent = JsonSerializer.Deserialize<TransactionRecordedEvent>(data.Span, JsonOptions);
            return transactionEvent is not null && transactionEvent.TransactionId != Guid.Empty;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryParseUtf8Json(string json, out TransactionRecordedEvent? transactionEvent)
    {
        transactionEvent = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0)
            {
                var first = document.RootElement[0];
                if (first.TryGetProperty("data", out var dataElement))
                {
                    transactionEvent = dataElement.Deserialize<TransactionRecordedEvent>(JsonOptions);
                    return transactionEvent is not null && transactionEvent.TransactionId != Guid.Empty;
                }
            }

            transactionEvent = JsonSerializer.Deserialize<TransactionRecordedEvent>(json, JsonOptions);
            return transactionEvent is not null && transactionEvent.TransactionId != Guid.Empty;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string DescribePayload(ReadOnlyMemory<byte> data) =>
        data.IsEmpty ? string.Empty : Encoding.UTF8.GetString(data.Span);
}
