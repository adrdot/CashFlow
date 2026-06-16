using System.Diagnostics.Metrics;

namespace CashFlow.Reporting.Infrastructure.Observability;

public sealed class ReportingMetrics
{
    public const string MeterName = "CashFlow.Reporting";

    public static class Tags
    {
        public const string Outcome = "outcome";
        public const string ErrorType = "error_type";
        public const string Format = "format";
        public const string Cache = "cache";
    }

    private readonly Histogram<double> readDurationMs;
    private readonly Counter<long> messagesConsumed;
    private readonly Counter<long> messageFailures;
    private readonly Histogram<double> projectionDurationMs;
    private readonly Histogram<double> pipelineDurationMs;
    private readonly Counter<long> cacheHits;
    private readonly Counter<long> cacheMisses;
    private readonly Counter<long> cacheInvalidations;
    private readonly Counter<long> exportSuccesses;
    private readonly Counter<long> exportFailures;

    public ReportingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        readDurationMs = meter.CreateHistogram<double>(
            "reporting.read.duration",
            unit: "ms",
            description: "Time to serve consolidated daily report reads."
        );

        messagesConsumed = meter.CreateCounter<long>(
            "reporting.messages.consumed",
            description: "SQS messages successfully projected into reporting-db."
        );

        messageFailures = meter.CreateCounter<long>(
            "reporting.messages.failures",
            description: "Failed SQS projection attempts."
        );

        projectionDurationMs = meter.CreateHistogram<double>(
            "reporting.projection.duration",
            unit: "ms",
            description: "Time to project one SQS message into reporting-db."
        );

        pipelineDurationMs = meter.CreateHistogram<double>(
            "reporting.pipeline.duration",
            unit: "ms",
            description: "Time from event creation to successful reporting projection."
        );

        cacheHits = meter.CreateCounter<long>(
            "reporting.cache.hits",
            description: "Daily report cache hits."
        );

        cacheMisses = meter.CreateCounter<long>(
            "reporting.cache.misses",
            description: "Daily report cache misses."
        );

        cacheInvalidations = meter.CreateCounter<long>(
            "reporting.cache.invalidations",
            description: "Daily report cache invalidations after projection updates."
        );

        exportSuccesses = meter.CreateCounter<long>(
            "reporting.export.successes",
            description: "Successful report export operations."
        );

        exportFailures = meter.CreateCounter<long>(
            "reporting.export.failures",
            description: "Failed report export operations."
        );
    }

    public void IncrementMessagesConsumed() => messagesConsumed.Add(1);

    public void RecordDailyReadDuration(TimeSpan duration, bool fromCache, string outcome) =>
        readDurationMs.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>(Tags.Cache, fromCache ? "hit" : "miss"),
            new KeyValuePair<string, object?>(Tags.Outcome, outcome)
        );

    public void IncrementMessageFailure(string? errorType = null)
    {
        if (string.IsNullOrWhiteSpace(errorType))
        {
            messageFailures.Add(1);
            return;
        }

        messageFailures.Add(1, new KeyValuePair<string, object?>(Tags.ErrorType, errorType));
    }

    public void RecordProjectionDuration(TimeSpan duration, string outcome) =>
        projectionDurationMs.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>(Tags.Outcome, outcome)
        );

    public void RecordPipelineDuration(TimeSpan duration) =>
        pipelineDurationMs.Record(duration.TotalMilliseconds);

    public void IncrementCacheHit() => cacheHits.Add(1);

    public void IncrementCacheMiss() => cacheMisses.Add(1);

    public void IncrementCacheInvalidation() => cacheInvalidations.Add(1);

    public void IncrementExportSuccess(string format) =>
        exportSuccesses.Add(1, new KeyValuePair<string, object?>(Tags.Format, format));

    public void IncrementExportFailure(string format, string? errorType = null)
    {
        exportFailures.Add(
            1,
            new KeyValuePair<string, object?>(Tags.Format, format),
            new KeyValuePair<string, object?>(Tags.ErrorType, errorType ?? "unknown")
        );
    }
}
