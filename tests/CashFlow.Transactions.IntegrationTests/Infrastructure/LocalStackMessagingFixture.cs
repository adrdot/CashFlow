using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Testcontainers.LocalStack;

namespace CashFlow.Transactions.IntegrationTests.Infrastructure;

public sealed class LocalStackMessagingFixture : IAsyncLifetime
{
    private const string Region = "us-east-1";
    private const string TopicName = "cashflow-transaction-recorded-test";
    private const string QueueName = "cashflow-transaction-recorded-test-queue";

    private LocalStackContainer? container;

    public bool IsAvailable { get; private set; }

    public string ServiceUrl { get; private set; } = string.Empty;

    public string TopicArn { get; private set; } = string.Empty;

    public string QueueUrl { get; private set; } = string.Empty;

    public IAmazonSimpleNotificationService CreateSnsClient()
        => new AmazonSimpleNotificationServiceClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = ServiceUrl,
                AuthenticationRegion = Region,
                UseHttp = true
            });

    public IAmazonSQS CreateSqsClient()
        => new AmazonSQSClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonSQSConfig
            {
                ServiceURL = ServiceUrl,
                AuthenticationRegion = Region,
                UseHttp = true
            });

    public async Task InitializeAsync()
    {
        if (!DockerTestHelper.IsDockerAvailable())
        {
            return;
        }

        try
        {
            container = new LocalStackBuilder()
                .WithImage("localstack/localstack:4")
                .WithEnvironment("SERVICES", "sns,sqs")
                .WithEnvironment("DEFAULT_REGION", Region)
                .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
                .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
                .Build();

            await container.StartAsync();

            ServiceUrl = container.GetConnectionString();
            await ProvisionMessagingAsync();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
            if (container is not null)
            {
                await container.DisposeAsync();
                container = null;
            }
        }
    }

    public async Task<string?> ReceiveMessageBodyAsync(TimeSpan timeout)
    {
        using var sqs = CreateSqsClient();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = QueueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 2
            });

            var message = response.Messages.FirstOrDefault();
            if (message is null)
            {
                continue;
            }

            await sqs.DeleteMessageAsync(QueueUrl, message.ReceiptHandle);
            return ExtractSnsPayload(message.Body);
        }

        return null;
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    private async Task ProvisionMessagingAsync()
    {
        using var sns = CreateSnsClient();
        using var sqs = CreateSqsClient();

        var topicResponse = await sns.CreateTopicAsync(TopicName);
        TopicArn = topicResponse.TopicArn;

        var queueResponse = await sqs.CreateQueueAsync(QueueName);
        QueueUrl = queueResponse.QueueUrl;

        var attributes = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = QueueUrl,
            AttributeNames = ["QueueArn"]
        });

        var queueArn = attributes.Attributes["QueueArn"];
        await sns.SubscribeAsync(new SubscribeRequest
        {
            TopicArn = TopicArn,
            Protocol = "sqs",
            Endpoint = queueArn
        });

        await sqs.SetQueueAttributesAsync(new SetQueueAttributesRequest
        {
            QueueUrl = QueueUrl,
            Attributes = new Dictionary<string, string>
            {
                ["Policy"] = $$"""
                {
                  "Version": "2012-10-17",
                  "Statement": [
                    {
                      "Effect": "Allow",
                      "Principal": { "Service": "sns.amazonaws.com" },
                      "Action": "sqs:SendMessage",
                      "Resource": "{{queueArn}}",
                      "Condition": {
                        "ArnEquals": { "aws:SourceArn": "{{TopicArn}}" }
                      }
                    }
                  ]
                }
                """
            }
        });
    }

    private static string ExtractSnsPayload(string body)
    {
        using var document = System.Text.Json.JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("Message", out var messageElement))
        {
            return messageElement.GetString() ?? body;
        }

        return body;
    }
}
