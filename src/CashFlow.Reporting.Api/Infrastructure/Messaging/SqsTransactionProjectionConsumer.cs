using System.Diagnostics;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using CashFlow.Reporting.Infrastructure.Messaging;
using CashFlow.Reporting.Infrastructure.Observability;
using CashFlow.Reporting.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashFlow.Reporting.Infrastructure.Messaging;

public sealed class SqsTransactionProjectionConsumer(
    IServiceScopeFactory scopeFactory,
    IAmazonSQS sqsClient,
    ReportingMetrics metrics,
    IOptions<ReportingMessagingOptions> options,
    ILogger<SqsTransactionProjectionConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SqsQueueUrl))
        {
            logger.LogWarning("SQS queue URL is not configured. Reporting projection consumer is idle.");
            return;
        }

        var queueUrl = LocalStackSqsUrlNormalizer.Normalize(settings.SqsQueueUrl, settings.ServiceUrl);

        logger.LogInformation(
            "Reporting projection consumer started for queue {QueueUrl} (service {ServiceUrl}).",
            queueUrl,
            settings.ServiceUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = settings.MaxMessages,
                    WaitTimeSeconds = settings.WaitTimeSeconds,
                    VisibilityTimeout = settings.VisibilityTimeoutSeconds,
                    MessageAttributeNames = ["All"]
                }, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(queueUrl, message, stoppingToken);
                }
            }
            catch (AmazonSQSException ex) when (IsQueueMissing(ex))
            {
                logger.LogWarning(
                    ex,
                    "SQS queue {QueueUrl} is not available yet. Retrying in 10 seconds.",
                    queueUrl);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected reporting projection consumer failure.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static bool IsQueueMissing(AmazonSQSException exception)
    {
        return exception is QueueDoesNotExistException
            || string.Equals(exception.ErrorCode, "AWS.SimpleQueueService.NonExistentQueue", StringComparison.Ordinal)
            || exception.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ProcessMessageAsync(string queueUrl, Message message, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<TransactionProjectionWriter>();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var transactionEvent = DeserializeEvent(message.Body);
            await writer.ProjectAsync(
                transactionEvent.TransactionId,
                transactionEvent.UserId,
                transactionEvent.Type,
                transactionEvent.Amount,
                transactionEvent.Description,
                transactionEvent.TransactionDate,
                transactionEvent.CreatedAtUtc,
                cancellationToken);

            await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, cancellationToken);

            metrics.IncrementMessagesConsumed();
            metrics.RecordProjectionDuration(stopwatch.Elapsed, "success");
            metrics.RecordPipelineDuration(DateTimeOffset.UtcNow - transactionEvent.CreatedAtUtc);
        }
        catch (Exception ex)
        {
            metrics.IncrementMessageFailure(ex.GetType().Name);
            metrics.RecordProjectionDuration(stopwatch.Elapsed, "failure");
            logger.LogError(ex, "Failed to project SQS message {MessageId}.", message.MessageId);
        }
    }

    private static TransactionRecordedMessage DeserializeEvent(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("Message", out var snsEnvelope))
        {
            return JsonSerializer.Deserialize<TransactionRecordedMessage>(snsEnvelope.GetString()!, JsonOptions)
                ?? throw new InvalidOperationException("SNS envelope message is invalid.");
        }

        return JsonSerializer.Deserialize<TransactionRecordedMessage>(body, JsonOptions)
            ?? throw new InvalidOperationException("SQS message body is invalid.");
    }
}

public sealed record TransactionRecordedMessage
{
    public Guid TransactionId { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateOnly TransactionDate { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}

public static class SqsClientFactory
{
    public static IAmazonSQS Create(ReportingMessagingOptions options)
    {
        var config = new AmazonSQSConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
        };

        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
            config.UseHttp = options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        var credentials = new BasicAWSCredentials("test", "test");
        return new AmazonSQSClient(credentials, config);
    }
}
