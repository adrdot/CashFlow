using System.Diagnostics.Metrics;
using System.Text.Json;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Observability;
using CashFlow.Transactions.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.IntegrationTests.Messaging;

[Trait("Category", "Docker")]
public sealed class SnsTransactionEventPublisherIntegrationTests(LocalStackMessagingFixture fixture)
    : IClassFixture<LocalStackMessagingFixture>
{
    [Fact]
    public async Task PublishAsync_DeliversTransactionEvent_ToSubscribedSqsQueue()
    {
        if (!fixture.IsAvailable)
        {
            return;
        }

        var publisher = CreatePublisher();
        var transactionEvent = new TransactionRecordedEvent
        {
            TransactionId = Guid.NewGuid(),
            UserId = "sns-integration@cashflow.local",
            Type = "Credit",
            Amount = 420.50m,
            Description = "LocalStack SNS integration",
            TransactionDate = new DateOnly(2026, 6, 14),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        await publisher.PublishAsync(transactionEvent);

        var payload = await fixture.ReceiveMessageBodyAsync(TimeSpan.FromSeconds(15));
        Assert.NotNull(payload);

        var received = JsonSerializer.Deserialize<TransactionRecordedEvent>(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
        Assert.NotNull(received);
        Assert.Equal(transactionEvent.TransactionId, received.TransactionId);
        Assert.Equal(transactionEvent.UserId, received.UserId);
        Assert.Equal(transactionEvent.Type, received.Type);
        Assert.Equal(transactionEvent.Amount, received.Amount);
    }

    private SnsTransactionEventPublisher CreatePublisher()
    {
        var options = Options.Create(
            new MessagingOptions
            {
                Enabled = true,
                SnsTopicArn = fixture.TopicArn,
                ServiceUrl = fixture.ServiceUrl,
                Region = "us-east-1",
            }
        );

        return new SnsTransactionEventPublisher(
            fixture.CreateSnsClient(),
            options,
            CreateTestMetrics(),
            NullLogger<SnsTransactionEventPublisher>.Instance
        );
    }

    private static TransactionMetrics CreateTestMetrics()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<RelaySubscriptionStats>();
        using var provider = services.BuildServiceProvider();
        return new TransactionMetrics(
            provider.GetRequiredService<IMeterFactory>(),
            provider.GetRequiredService<RelaySubscriptionStats>()
        );
    }
}
