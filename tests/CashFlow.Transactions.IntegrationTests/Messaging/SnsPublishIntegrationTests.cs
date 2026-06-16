using System.Diagnostics.Metrics;
using System.Text.Json;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Messaging.Abstractions;
using CashFlow.Transactions.Infrastructure.Observability;
using CashFlow.Transactions.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.IntegrationTests.Messaging;

[Trait("Category", "Docker")]
public sealed class SnsPublishIntegrationTests(LocalStackMessagingFixture localStack)
    : IClassFixture<LocalStackMessagingFixture>
{
    [Fact]
    public async Task SnsPublisher_PublishesTransactionEvent_ToLocalStackSns()
    {
        if (!localStack.IsAvailable)
        {
            return;
        }

        var transactionId = Guid.NewGuid();
        var recordedEvent = new TransactionRecordedEvent
        {
            TransactionId = transactionId,
            UserId = "relay-integration@cashflow.local",
            Type = "Debit",
            Amount = 88.00m,
            Description = "SNS publish LocalStack test",
            TransactionDate = new DateOnly(2026, 6, 14),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        ITransactionEventPublisher publisher = new SnsTransactionEventPublisher(
            localStack.CreateSnsClient(),
            Options.Create(
                new MessagingOptions
                {
                    Enabled = true,
                    SnsTopicArn = localStack.TopicArn,
                    ServiceUrl = localStack.ServiceUrl,
                    Region = "us-east-1",
                }
            ),
            CreateTestMetrics(),
            NullLogger<SnsTransactionEventPublisher>.Instance
        );

        await publisher.PublishAsync(recordedEvent);

        var payload = await localStack.ReceiveMessageBodyAsync(TimeSpan.FromSeconds(15));
        Assert.NotNull(payload);
        Assert.Contains(transactionId.ToString(), payload, StringComparison.OrdinalIgnoreCase);
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
