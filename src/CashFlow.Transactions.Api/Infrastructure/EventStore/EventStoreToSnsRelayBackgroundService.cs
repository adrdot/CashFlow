using System.Diagnostics;
using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Observability;
using global::EventStore.Client;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersistentSubscriptionResult = EventStore.Client.EventStorePersistentSubscriptionsClient.PersistentSubscriptionResult;

namespace CashFlow.Transactions.Infrastructure.EventStore;

public sealed class EventStoreToSnsRelayBackgroundService(
    EventStorePersistentSubscriptionsClient persistentSubscriptionsClient,
    IServiceScopeFactory scopeFactory,
    IOptions<EventStoreOptions> eventStoreOptions,
    IOptions<MessagingOptions> messagingOptions,
    ILogger<EventStoreToSnsRelayBackgroundService> logger) : BackgroundService
{
    private const int SubscriptionBufferSize = 20;

    private readonly EventStoreOptions _eventStoreOptions = eventStoreOptions.Value;
    private readonly MessagingOptions _messagingOptions = messagingOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_messagingOptions.Enabled)
        {
            logger.LogInformation("EventStore SNS relay is disabled (messaging off).");
            return;
        }

        var groupName = _eventStoreOptions.SubscriptionGroupName;
        logger.LogInformation(
            "EventStore SNS relay starting (group {GroupName}, stream prefix {StreamPrefix}).",
            groupName,
            _eventStoreOptions.StreamNamePrefix);

        await EnsurePersistentSubscriptionAsync(groupName, stoppingToken);

        await using var subscription = persistentSubscriptionsClient.SubscribeToAll(
            groupName,
            SubscriptionBufferSize,
            userCredentials: null,
            cancellationToken: stoppingToken);

        try
        {
            await foreach (var resolvedEvent in subscription.WithCancellation(stoppingToken))
            {
                await ProcessEventAsync(subscription, resolvedEvent, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task EnsurePersistentSubscriptionAsync(string groupName, CancellationToken cancellationToken)
    {
        var settings = new PersistentSubscriptionSettings(
            resolveLinkTos: false,
            startFrom: Position.Start);

        var filter = StreamFilter.Prefix(_eventStoreOptions.StreamNamePrefix);

        try
        {
            await persistentSubscriptionsClient.CreateToAllAsync(
                groupName,
                filter,
                settings,
                cancellationToken: cancellationToken);
            logger.LogInformation("Created persistent subscription group {GroupName}.", groupName);
        }
        catch (Exception ex) when (IsPersistentSubscriptionGroupAlreadyExists(ex))
        {
            logger.LogDebug("Persistent subscription group {GroupName} already exists.", groupName);
        }
    }

    private static bool IsPersistentSubscriptionGroupAlreadyExists(Exception exception) =>
        exception is DuplicateKeyException
        || (exception is RpcException { StatusCode: StatusCode.AlreadyExists });

    private async Task ProcessEventAsync(
        PersistentSubscriptionResult subscription,
        ResolvedEvent resolvedEvent,
        CancellationToken cancellationToken)
    {
        if (resolvedEvent.Event.EventType is null
            || !TransactionRecordedEventParser.TryParse(
                resolvedEvent.Event.Data,
                resolvedEvent.Event.EventType,
                out var transactionEvent)
            || transactionEvent is null)
        {
            await subscription.Ack(resolvedEvent);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<ITransactionEventPublisher>();
        var metrics = scope.ServiceProvider.GetRequiredService<TransactionMetrics>();

        var publishStopwatch = Stopwatch.StartNew();
        try
        {
            await publisher.PublishAsync(transactionEvent, cancellationToken);
            metrics.IncrementPublishedEvents();
            metrics.RecordEndToEndDuration(DateTimeOffset.UtcNow - transactionEvent.CreatedAtUtc);
            metrics.RecordPublishDuration(publishStopwatch.Elapsed, "success");

            await subscription.Ack(resolvedEvent);

            logger.LogInformation(
                TransactionLogEvents.OutboxEventPublished,
                "Relayed EventStore event {TransactionId} to SNS in {DurationMs}ms.",
                transactionEvent.TransactionId,
                publishStopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            metrics.IncrementPublishFailure(ex.GetType().Name);
            metrics.RecordPublishDuration(publishStopwatch.Elapsed, "failure");
            logger.LogWarning(
                TransactionLogEvents.OutboxPublishFailed,
                ex,
                "Failed to relay EventStore event {TransactionId} to SNS.",
                transactionEvent.TransactionId);

            await subscription.Nack(
                PersistentSubscriptionNakEventAction.Retry,
                ex.ToString(),
                resolvedEvent);
        }
    }
}
