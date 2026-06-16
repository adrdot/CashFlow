using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Infrastructure.EventStore;

public sealed class EventStoreRelaySubscriptionMonitorBackgroundService(
    EventStorePersistentSubscriptionStatsReader statsReader,
    RelaySubscriptionStats relaySubscriptionStats,
    IOptions<EventStoreOptions> eventStoreOptions,
    IOptions<MessagingOptions> messagingOptions,
    ILogger<EventStoreRelaySubscriptionMonitorBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!messagingOptions.Value.Enabled)
        {
            return;
        }

        var groupName = eventStoreOptions.Value.SubscriptionGroupName;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var stats = await statsReader.TryGetToAllStatsAsync(groupName, stoppingToken);
                if (stats is { } snapshot)
                {
                    relaySubscriptionStats.Update(
                        snapshot.LagEvents,
                        snapshot.InFlightMessages,
                        snapshot.ParkedMessages
                    );
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Failed to poll EventStore subscription stats for {GroupName}.",
                    groupName
                );
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(eventStoreOptions.Value.SubscriptionMonitorPollSeconds),
                    stoppingToken
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
