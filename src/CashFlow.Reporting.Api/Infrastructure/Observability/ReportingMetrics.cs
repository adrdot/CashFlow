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
        public const string Method = "method";
        public const string Route = "route";
        public const string StatusClass = "status_class";
        public const string Cache = "cache";
    }

    private readonly Counter<long> httpRequests;
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

    public ReportingMetrics(IMeterFactory meterFactory, ReportingQueueStats queueStats)
    {
        var meter = meterFactory.Create(MeterName);

        httpRequests = meter.CreateCounter<long>(
            "reporting.requests.total",
            description: "HTTP requests handled by the reporting API.");

        readDurationMs = meter.CreateHistogram<double>(
            "reporting.read.duration",
            unit: "ms",
            description: "Time to serve consolidated daily report reads.");

        messagesConsumed = meter.CreateCounter<long>(
            "reporting.messages.consumed",
            description: "SQS messages successfully projected into reporting-db.");

        messageFailures = meter.CreateCounter<long>(
            "reporting.messages.failures",
            description: "Failed SQS projection attempts.");

        projectionDurationMs = meter.CreateHistogram<double>(
            "reporting.projection.duration",
            unit: "ms",
            description: "Time to project one SQS message into reporting-db.");

        pipelineDurationMs = meter.CreateHistogram<double>(
            "reporting.pipeline.duration",
            unit: "ms",
            description: "Time from event creation to successful reporting projection.");

        cacheHits = meter.CreateCounter<long>(
            "reporting.cache.hits",
            description: "Daily report cache hits.");

        cacheMisses = meter.CreateCounter<long>(
            "reporting.cache.misses",
            description: "Daily report cache misses.");

        cacheInvalidations = meter.CreateCounter<long>(
            "reporting.cache.invalidations",
            description: "Daily report cache invalidations after projection updates.");

        exportSuccesses = meter.CreateCounter<long>(
            "reporting.export.successes",
            description: "Successful report export operations.");

        exportFailures = meter.CreateCounter<long>(
            "reporting.export.failures",
            description: "Failed report export operations.");

        meter.CreateObservableGauge(
            "reporting.sqs.visible_messages",
            () => queueStats.VisibleMessages,
            description: "Approximate visible messages in the reporting SQS queue.");

        meter.CreateObservableGauge(
            "reporting.sqs.in_flight_messages",
            () => queueStats.InFlightMessages,
            description: "Approximate in-flight messages in the reporting SQS queue.");
    }

    public void IncrementMessagesConsumed() => messagesConsumed.Add(1);

    public void RecordHttpRequest(string method, string route, int statusCode) =>
        httpRequests.Add(1,
            new KeyValuePair<string, object?>(Tags.Method, method),
            new KeyValuePair<string, object?>(Tags.Route, route),
            new KeyValuePair<string, object?>(Tags.StatusClass, ToStatusClass(statusCode)));

    public void RecordDailyReadDuration(TimeSpan duration, bool fromCache, string outcome) =>
        readDurationMs.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>(Tags.Cache, fromCache ? "hit" : "miss"),
            new KeyValuePair<string, object?>(Tags.Outcome, outcome));

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
            new KeyValuePair<string, object?>(Tags.Outcome, outcome));

    public void RecordPipelineDuration(TimeSpan duration) =>
        pipelineDurationMs.Record(duration.TotalMilliseconds);

    public void IncrementCacheHit() => cacheHits.Add(1);

    public void IncrementCacheMiss() => cacheMisses.Add(1);

    public void IncrementCacheInvalidation() => cacheInvalidations.Add(1);

    public void IncrementExportSuccess(string format) =>
        exportSuccesses.Add(1, new KeyValuePair<string, object?>(Tags.Format, format));

    public void IncrementExportFailure(string format, string? errorType = null)
    {
        exportFailures.Add(1,
            new KeyValuePair<string, object?>(Tags.Format, format),
            new KeyValuePair<string, object?>(Tags.ErrorType, errorType ?? "unknown"));
    }

    private static string ToStatusClass(int statusCode) => statusCode switch
    {
        >= 500 => "5xx",
        >= 400 => "4xx",
        >= 300 => "3xx",
        >= 200 => "2xx",
        _ => "other"
    };
}
