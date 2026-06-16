using CashFlow.Transactions.Api.Observability;

namespace CashFlow.Transactions.UnitTests.Observability;

public sealed class TransactionAlertCatalogTests
{
    [Fact]
    public void PromQlHelpers_ContainTransactionMetricNames()
    {
        Assert.Contains("transactions_persistence_duration", TransactionAlertCatalog.PersistenceP95PromQl());
        Assert.Contains("transactions_end_to_end_duration", TransactionAlertCatalog.EndToEndP95PromQl());
        Assert.Contains("transactions_requests_total", TransactionAlertCatalog.ServerErrorRatePromQl());
        Assert.Contains("transactions_events_publish_failures", TransactionAlertCatalog.PublishFailuresSustainedPromQl);
    }

    [Fact]
    public void SloConstants_MatchDocumentedTargets()
    {
        Assert.Equal(200, TransactionSlo.MaxPersistenceLatencyMs);
        Assert.Equal(1.0, TransactionSlo.MaxServerErrorPercent);
        Assert.Equal(5, TransactionSlo.EndToEndRecordingSeconds);
        Assert.Equal(95.0, TransactionSlo.EndToEndRecordingPercentile);
        Assert.Equal(5, TransactionSlo.PublishFailureSustainedMinutes);
    }
}
