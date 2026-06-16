using System.Diagnostics.Metrics;
using CashFlow.Reporting.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Reporting.UnitTests.Observability;

public sealed class ReportingMetricsTests
{
    [Fact]
    public void IncrementMessagesConsumed_RecordsCounter()
    {
        using var harness = new MetricsTestHarness();
        var metrics = harness.CreateMetrics();

        metrics.IncrementMessagesConsumed();

        var measurement = Assert.Single(
            harness.SnapshotMeasurements(),
            m => m.InstrumentName == "reporting.messages.consumed"
        );
        Assert.Equal(1L, measurement.Value);
    }

    [Fact]
    public void RecordProjectionDuration_RecordsOutcomeTag()
    {
        using var harness = new MetricsTestHarness();
        var metrics = harness.CreateMetrics();

        metrics.RecordProjectionDuration(TimeSpan.FromMilliseconds(25), "success");

        var measurement = Assert.Single(
            harness.SnapshotMeasurements(),
            m => m.InstrumentName == "reporting.projection.duration"
        );
        Assert.Contains(
            measurement.Tags,
            tag => tag.Key == ReportingMetrics.Tags.Outcome && (string?)tag.Value == "success"
        );
    }

    [Fact]
    public void RecordDailyReadDuration_RecordsCacheTag()
    {
        using var harness = new MetricsTestHarness();
        var metrics = harness.CreateMetrics();

        metrics.RecordDailyReadDuration(TimeSpan.FromMilliseconds(12), fromCache: true, "success");

        var measurement = Assert.Single(
            harness.SnapshotMeasurements(),
            m => m.InstrumentName == "reporting.read.duration"
        );
        Assert.Contains(
            measurement.Tags,
            tag => tag.Key == ReportingMetrics.Tags.Cache && (string?)tag.Value == "hit"
        );
    }

    private sealed class MetricsTestHarness : IDisposable
    {
        private readonly object sync = new();
        private readonly MeterListener listener = new();
        private readonly ServiceProvider provider;

        private readonly List<RecordedMeasurement> measurements = [];

        public IReadOnlyList<RecordedMeasurement> SnapshotMeasurements()
        {
            lock (sync)
            {
                return measurements.ToArray();
            }
        }

        public MetricsTestHarness()
        {
            var services = new ServiceCollection();
            services.AddMetrics();
            provider = services.BuildServiceProvider();

            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == ReportingMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };

            listener.SetMeasurementEventCallback<long>(RecordMeasurement);
            listener.SetMeasurementEventCallback<double>(RecordMeasurement);
            listener.Start();
        }

        public ReportingMetrics CreateMetrics() =>
            new(provider.GetRequiredService<IMeterFactory>());

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
            lock (sync)
            {
                measurements.Add(
                    new RecordedMeasurement(instrument.Name, measurement!, tags.ToArray())
                );
            }
        }
    }

    private sealed record RecordedMeasurement(
        string InstrumentName,
        object Value,
        KeyValuePair<string, object?>[] Tags
    );
}
