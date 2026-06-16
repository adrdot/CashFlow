using Amazon.SimpleNotificationService;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Api.HealthChecks;

public sealed class EventStoreHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("eventstore");
            var response = await client.GetAsync("/health/live", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("EventStoreDB is reachable.")
                : HealthCheckResult.Unhealthy($"EventStoreDB returned {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("EventStoreDB health check failed.", ex);
        }
    }
}

public sealed class SnsHealthCheck(
    IAmazonSimpleNotificationService snsClient,
    IOptions<MessagingOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.SnsTopicArn))
        {
            return HealthCheckResult.Degraded("SNS messaging is not configured.");
        }

        try
        {
            await snsClient.GetTopicAttributesAsync(settings.SnsTopicArn, cancellationToken);
            return HealthCheckResult.Healthy("SNS topic is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SNS health check failed.", ex);
        }
    }
}
