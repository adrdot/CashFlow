using Amazon.SQS;
using Amazon.SQS.Model;
using CashFlow.Reporting.Infrastructure.Messaging;
using CashFlow.Reporting.Infrastructure.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashFlow.Reporting.Infrastructure.Messaging;

public sealed class ReportingSqsQueueMonitorBackgroundService(
    IAmazonSQS sqsClient,
    ReportingQueueStats queueStats,
    IOptions<ReportingMessagingOptions> options,
    ILogger<ReportingSqsQueueMonitorBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SqsQueueUrl))
        {
            return;
        }

        var queueUrl = LocalStackSqsUrlNormalizer.Normalize(settings.SqsQueueUrl, settings.ServiceUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
                {
                    QueueUrl = queueUrl,
                    AttributeNames =
                    [
                        QueueAttributeName.ApproximateNumberOfMessages,
                        QueueAttributeName.ApproximateNumberOfMessagesNotVisible
                    ]
                }, stoppingToken);

                var visible = ParseAttribute(response, QueueAttributeName.ApproximateNumberOfMessages);
                var inFlight = ParseAttribute(response, QueueAttributeName.ApproximateNumberOfMessagesNotVisible);
                queueStats.Update(visible, inFlight);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to poll SQS queue depth for {QueueUrl}.", queueUrl);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static long ParseAttribute(GetQueueAttributesResponse response, QueueAttributeName attributeName)
    {
        if (!response.Attributes.TryGetValue(attributeName, out var value)
            || !long.TryParse(value, out var parsed))
        {
            return 0;
        }

        return parsed;
    }
}
