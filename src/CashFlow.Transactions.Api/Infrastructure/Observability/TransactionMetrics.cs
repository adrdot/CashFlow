using System.Diagnostics.Metrics;



namespace CashFlow.Transactions.Infrastructure.Observability;



public sealed class TransactionMetrics

{

    public const string MeterName = "CashFlow.Transactions";



    public static class Tags

    {

        public const string Type = "type";

        public const string Stage = "stage";

        public const string Outcome = "outcome";

        public const string Method = "method";

        public const string Route = "route";

        public const string StatusClass = "status_class";

        public const string ErrorType = "error_type";

    }



    private readonly Counter<long> createdTransactions;

    private readonly Counter<long> idempotentReplays;

    private readonly Counter<long> persistenceFailures;

    private readonly Counter<long> publishedEvents;

    private readonly Counter<long> publishFailures;

    private readonly Counter<long> httpRequests;

    private readonly Histogram<double> persistenceDurationMs;

    private readonly Histogram<double> eventStoreAppendDurationMs;

    private readonly Histogram<double> publishDurationMs;

    private readonly Histogram<double> endToEndDurationMs;



    public TransactionMetrics(IMeterFactory meterFactory, RelaySubscriptionStats relaySubscriptionStats)

    {

        var meter = meterFactory.Create(MeterName);



        createdTransactions = meter.CreateCounter<long>(

            "transactions.created",

            description: "Transactions successfully persisted to EventStore.");



        idempotentReplays = meter.CreateCounter<long>(

            "transactions.idempotent_replays",

            description: "Idempotent replays served for duplicate Idempotency-Key requests.");



        persistenceFailures = meter.CreateCounter<long>(

            "transactions.persistence.failures",

            description: "Transaction persistence failures by infrastructure stage.");



        publishedEvents = meter.CreateCounter<long>(

            "transactions.events.published",

            description: "Transaction events successfully published to SNS.");



        publishFailures = meter.CreateCounter<long>(

            "transactions.events.publish_failures",

            description: "Failed SNS publish attempts from the EventStore relay.");



        httpRequests = meter.CreateCounter<long>(

            "transactions.requests.total",

            description: "HTTP requests handled by the transactions API.");



        persistenceDurationMs = meter.CreateHistogram<double>(

            "transactions.persistence.duration",

            unit: "ms",

            description: "Time to persist a transaction to EventStore.");



        eventStoreAppendDurationMs = meter.CreateHistogram<double>(

            "transactions.eventstore.append.duration",

            unit: "ms",

            description: "Time to append a transaction event to EventStore.");



        publishDurationMs = meter.CreateHistogram<double>(

            "transactions.publish.duration",

            unit: "ms",

            description: "Time to publish a transaction event to SNS.");



        endToEndDurationMs = meter.CreateHistogram<double>(

            "transactions.end_to_end.duration",

            unit: "ms",

            description: "Time from event creation to successful SNS publish.");



        meter.CreateObservableGauge(

            "transactions.relay.subscription_lag",

            () => relaySubscriptionStats.LagEvents,

            unit: "events",

            description: "EventStore persistent subscription lag (lastKnown - lastProcessed).");



        meter.CreateObservableGauge(

            "transactions.relay.subscription_in_flight",

            () => relaySubscriptionStats.InFlightMessages,

            description: "In-flight messages in the EventStore relay subscription.");



        meter.CreateObservableGauge(

            "transactions.relay.parked_messages",

            () => relaySubscriptionStats.ParkedMessages,

            description: "Parked messages in the EventStore relay subscription.");

    }



    public void IncrementCreatedTransactions(string transactionType)

    {

        createdTransactions.Add(1, new KeyValuePair<string, object?>(Tags.Type, NormalizeType(transactionType)));

    }



    public void IncrementIdempotentReplays() => idempotentReplays.Add(1);



    public void IncrementPersistenceFailure(string stage) =>

        persistenceFailures.Add(1, new KeyValuePair<string, object?>(Tags.Stage, stage));



    public void IncrementPublishedEvents() => publishedEvents.Add(1);



    public void IncrementPublishFailure(string? errorType = null)

    {

        if (string.IsNullOrWhiteSpace(errorType))

        {

            publishFailures.Add(1);

            return;

        }



        publishFailures.Add(1, new KeyValuePair<string, object?>(Tags.ErrorType, errorType));

    }



    public void RecordHttpRequest(string method, string route, int statusCode)

    {

        httpRequests.Add(1,

            new KeyValuePair<string, object?>(Tags.Method, method),

            new KeyValuePair<string, object?>(Tags.Route, route),

            new KeyValuePair<string, object?>(Tags.StatusClass, ToStatusClass(statusCode)));

    }



    public void RecordPersistenceDuration(TimeSpan duration, string outcome) =>

        persistenceDurationMs.Record(

            duration.TotalMilliseconds,

            new KeyValuePair<string, object?>(Tags.Outcome, outcome));



    public void RecordEventStoreAppendDuration(TimeSpan duration, string outcome) =>

        eventStoreAppendDurationMs.Record(

            duration.TotalMilliseconds,

            new KeyValuePair<string, object?>(Tags.Outcome, outcome));



    public void RecordPublishDuration(TimeSpan duration, string outcome) =>

        publishDurationMs.Record(

            duration.TotalMilliseconds,

            new KeyValuePair<string, object?>(Tags.Outcome, outcome));



    public void RecordEndToEndDuration(TimeSpan duration) =>

        endToEndDurationMs.Record(duration.TotalMilliseconds);



    private static string NormalizeType(string transactionType) =>

        transactionType.Trim().ToLowerInvariant();



    private static string ToStatusClass(int statusCode) => statusCode switch

    {

        >= 500 => "5xx",

        >= 400 => "4xx",

        >= 300 => "3xx",

        >= 200 => "2xx",

        _ => "other"

    };

}

