using System.Net;
using CashFlow.Transactions.ContractTests.Infrastructure;
using CashFlow.Transactions.Infrastructure.Observability;

namespace CashFlow.Transactions.ContractTests;

public sealed class MetricsEndpointContractTests
{
    [Fact]
    public async Task MetricsEndpoint_ExposesTransactionsInstruments()
    {
        await using var factory = new TransactionsWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var metrics = scope.ServiceProvider.GetRequiredService<TransactionMetrics>();
        metrics.IncrementCreatedTransactions("debit");
        metrics.IncrementPublishedEvents();
        metrics.RecordPersistenceDuration(TimeSpan.FromMilliseconds(1), "success");
        scope.ServiceProvider.GetRequiredService<RelaySubscriptionStats>().Update(5, 2, 0);

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("transactions_created_total", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "transactions_events_published_total",
            body,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains(
            "transactions_relay_subscription_lag",
            body,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains(
            "transactions_persistence_duration_milliseconds",
            body,
            StringComparison.OrdinalIgnoreCase
        );
    }
}
