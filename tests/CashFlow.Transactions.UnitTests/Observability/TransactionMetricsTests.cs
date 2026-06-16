using System.Diagnostics.Metrics;
using CashFlow.Transactions.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transactions.UnitTests.Observability;

public sealed class TransactionMetricsTests
{
    [Fact]
    public void IncrementCreatedTransactions_RecordsCounterWithTypeTag()
    {
        using var harness = new MetricsTestHarness();
        var metrics = harness.CreateMetrics();

        metrics.IncrementCreatedTransactions("Debit");

        var measurement = Assert.Single(harness.Measurements);
        Assert.Equal("transactions.created", measurement.InstrumentName);
        Assert.Equal(1L, measurement.Value);
        Assert.Contains(
            measurement.Tags,
            tag => tag.Key == TransactionMetrics.Tags.Type && (string?)tag.Value == "debit"
        );
    }

    [Fact]
    public void IncrementPersistenceFailure_RecordsStageTag()
    {
        using var harness = new MetricsTestHarness();
        var metrics = harness.CreateMetrics();

        metrics.IncrementPersistenceFailure("eventstore");

        var measurement = Assert.Single(harness.Measurements);
        Assert.Equal("transactions.persistence.failures", measurement.InstrumentName);
        Assert.Contains(
            measurement.Tags,
            tag => tag.Key == TransactionMetrics.Tags.Stage && (string?)tag.Value == "eventstore"
        );
    }

    [Fact]
    public void RecordPersistenceDuration_RecordsOutcomeTag()
    {
        using var harness = new MetricsTestHarness();
        var metrics = harness.CreateMetrics();

        metrics.RecordPersistenceDuration(TimeSpan.FromMilliseconds(42), "success");

        var measurement = Assert.Single(harness.Measurements);
        Assert.Equal("transactions.persistence.duration", measurement.InstrumentName);
        Assert.Equal(42d, measurement.Value);
        Assert.Contains(
            measurement.Tags,
            tag => tag.Key == TransactionMetrics.Tags.Outcome && (string?)tag.Value == "success"
        );
    }

    [Fact]
    public void RecordPublishDuration_RecordsOutcomeTag()
    {
        using var harness = new MetricsTestHarness();
        var metrics = harness.CreateMetrics();

        metrics.RecordPublishDuration(TimeSpan.FromMilliseconds(12), "success");

        var measurement = Assert.Single(harness.Measurements);
        Assert.Equal("transactions.publish.duration", measurement.InstrumentName);
        Assert.Contains(
            measurement.Tags,
            tag => tag.Key == TransactionMetrics.Tags.Outcome && (string?)tag.Value == "success"
        );
    }

    private sealed class MetricsTestHarness : IDisposable
    {
        private readonly MeterListener listener = new();
        private readonly ServiceProvider provider;

        public List<RecordedMeasurement> Measurements { get; } = [];

        public MetricsTestHarness()
        {
            var services = new ServiceCollection();
            services.AddMetrics();
            services.AddSingleton<RelaySubscriptionStats>();
            provider = services.BuildServiceProvider();

            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == TransactionMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };

            listener.SetMeasurementEventCallback<long>(RecordMeasurement);
            listener.SetMeasurementEventCallback<double>(RecordMeasurement);
            listener.Start();
        }

        public TransactionMetrics CreateMetrics()
        {
            var relayStats = provider.GetRequiredService<RelaySubscriptionStats>();
            return new TransactionMetrics(provider.GetRequiredService<IMeterFactory>(), relayStats);
        }

        public void Dispose()
        {
            listener.Dispose();
            provider.Dispose();
        }

        private void RecordMeasurement<T>(
            Instrument instrument,
            T measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state
        )
            where T : struct
        {
            Measurements.Add(
                new RecordedMeasurement(instrument.Name, measurement!, tags.ToArray())
            );
        }
    }

    private sealed record RecordedMeasurement(
        string InstrumentName,
        object Value,
        KeyValuePair<string, object?>[] Tags
    );
}
