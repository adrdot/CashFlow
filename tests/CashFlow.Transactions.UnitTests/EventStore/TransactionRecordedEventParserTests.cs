using System.Text;
using System.Text.Json;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.EventStore;

namespace CashFlow.Transactions.UnitTests.EventStore;

public sealed class TransactionRecordedEventParserTests
{
    private static readonly TransactionRecordedEvent Sample = new()
    {
        TransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        UserId = "user-1",
        Type = "Debit",
        Amount = 42.50m,
        Description = "Test",
        TransactionDate = new DateOnly(2026, 6, 14),
        CreatedAtUtc = DateTimeOffset.Parse("2026-06-14T12:00:00Z"),
    };

    [Fact]
    public void TryParse_ReturnsTrue_ForTransactionRecordedPayload()
    {
        var json = JsonSerializer.Serialize(Sample);
        var data = Encoding.UTF8.GetBytes(json);

        var parsed = TransactionRecordedEventParser.TryParse(
            data,
            "TransactionRecorded",
            out var transactionEvent
        );

        Assert.True(parsed);
        Assert.NotNull(transactionEvent);
        Assert.Equal(Sample.TransactionId, transactionEvent.TransactionId);
        Assert.Equal(Sample.UserId, transactionEvent.UserId);
        Assert.Equal(Sample.Amount, transactionEvent.Amount);
    }

    [Theory]
    [InlineData("OtherEventType")]
    [InlineData("")]
    public void TryParse_ReturnsFalse_ForNonMatchingEventType(string eventType)
    {
        var data = Encoding.UTF8.GetBytes("{}");

        var parsed = TransactionRecordedEventParser.TryParse(
            data,
            eventType,
            out var transactionEvent
        );

        Assert.False(parsed);
        Assert.Null(transactionEvent);
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForInvalidJson()
    {
        var data = Encoding.UTF8.GetBytes("{ not-json");

        var parsed = TransactionRecordedEventParser.TryParse(
            data,
            "TransactionRecorded",
            out var transactionEvent
        );

        Assert.False(parsed);
        Assert.Null(transactionEvent);
    }
}
