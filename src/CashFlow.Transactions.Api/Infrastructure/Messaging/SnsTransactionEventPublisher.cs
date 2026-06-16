using System.Diagnostics;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using CashFlow.Transactions.Infrastructure.Messaging.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Infrastructure.Messaging;

public sealed class SnsTransactionEventPublisher : ITransactionEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAmazonSimpleNotificationService snsClient;
    private readonly MessagingOptions options;
    private readonly TransactionMetrics metrics;
    private readonly ILogger<SnsTransactionEventPublisher> logger;

    public SnsTransactionEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<MessagingOptions> options,
        TransactionMetrics metrics,
        ILogger<SnsTransactionEventPublisher> logger
    )
    {
        this.snsClient = snsClient;
        this.options = options.Value;
        this.metrics = metrics;
        this.logger = logger;
    }

    public async Task PublishAsync(
        TransactionRecordedEvent transactionEvent,
        CancellationToken cancellationToken = default
    )
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.SnsTopicArn))
        {
            logger.LogWarning("SNS publishing is disabled or topic ARN is missing.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var succeeded = false;

        try
        {
            var payload = JsonSerializer.Serialize(transactionEvent, JsonOptions);
            var request = new PublishRequest
            {
                TopicArn = options.SnsTopicArn,
                Message = payload,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["EventType"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = "TransactionRecorded",
                    },
                    ["TransactionId"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = transactionEvent.TransactionId.ToString(),
                    },
                },
            };

            await snsClient.PublishAsync(request, cancellationToken);
            succeeded = true;
            logger.LogInformation(
                TransactionLogEvents.OutboxEventPublished,
                "Published transaction event {TransactionId} to SNS topic.",
                transactionEvent.TransactionId
            );
        }
        finally
        {
            metrics.RecordPublishDuration(stopwatch.Elapsed, succeeded ? "success" : "failure");
        }
    }
}
