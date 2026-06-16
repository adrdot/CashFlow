using CashFlow.Transactions.Infrastructure.EventStore;

namespace CashFlow.Transactions.UnitTests.EventStore;

public sealed class EventStorePersistentSubscriptionStatsReaderTests
{
    [Fact]
    public void TryParseStats_ReadsLagAndInFlight_FromInfoPayload()
    {
        const string json = """
            {
              "groupName": "cashflow-sns-relay",
              "lastKnownEventNumber": 120,
              "lastProcessedEventNumber": 100,
              "totalInFlightMessages": 3,
              "parkedMessageCount": 1
            }
            """;

        var stats = EventStorePersistentSubscriptionStatsReader.TryParseStats(
            json,
            "cashflow-sns-relay"
        );

        Assert.NotNull(stats);
        Assert.Equal(20, stats.Value.LagEvents);
        Assert.Equal(3, stats.Value.InFlightMessages);
        Assert.Equal(1, stats.Value.ParkedMessages);
    }

    [Fact]
    public void TryParseStats_FindsGroup_InListPayload()
    {
        const string json = """
            [
              {
                "groupName": "other-group",
                "lastKnownEventNumber": 10,
                "lastProcessedEventNumber": 10
              },
              {
                "groupName": "cashflow-sns-relay",
                "lastKnownEventNumber": 55,
                "lastProcessedEventNumber": 50,
                "totalInFlightMessages": 2
              }
            ]
            """;

        var stats = EventStorePersistentSubscriptionStatsReader.TryParseStats(
            json,
            "cashflow-sns-relay"
        );

        Assert.NotNull(stats);
        Assert.Equal(5, stats.Value.LagEvents);
        Assert.Equal(2, stats.Value.InFlightMessages);
    }
}
