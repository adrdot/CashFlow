using System.Net;
using System.Net.Http.Json;
using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transactions.IntegrationTests;

/// <summary>
/// NFR: lançamentos permanecem disponíveis quando reporting-api / reporting-worker estão indisponíveis.
/// A Transactions API não registra dependências de reporting no pipeline HTTP.
/// </summary>
public sealed class ReportingAvailabilityIsolationTests : IAsyncLifetime
{
    private TransactionsWebApplicationFactory? factory;

    public Task InitializeAsync()
    {
        factory = new TransactionsWebApplicationFactory(TransactionsTestMode.EventStorePersistence);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateTransaction_Succeeds_WhenReportingServicesAreUnavailable()
    {
        if (factory is null)
        {
            return;
        }

        using var client = factory.CreateClient();
        TestJwtTokenHelper.AuthorizeClient(client);

        var response = await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
        {
            Type = "Credit",
            Amount = 150m,
            Description = "Isolation test credit",
            TransactionDate = new DateOnly(2026, 6, 15)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateTransactionResult>();
        Assert.NotNull(payload);
        Assert.True(payload.Succeeded);
        Assert.NotNull(payload.Transaction);

        Assert.NotNull(factory.EventStore);
        Assert.Single(factory.EventStore.AppendedEvents);
    }

    [Fact]
    public void TransactionsApi_ProjectDoesNotReferenceReportingAssembly()
    {
        var assembly = typeof(CashFlow.Transactions.Application.UseCases.CreateTransactionHandler).Assembly;
        var referencesReporting = assembly.GetReferencedAssemblies()
            .Any(static a => a.Name?.StartsWith("CashFlow.Reporting", StringComparison.OrdinalIgnoreCase) == true);

        Assert.False(referencesReporting);
    }

    [Fact]
    public void TransactionsApi_UsesNullEventPublisher_OnWritePath()
    {
        using var factory = new TransactionsWebApplicationFactory(TransactionsTestMode.EventStorePersistence);
        using var scope = factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<ITransactionEventPublisher>();

        Assert.IsType<NullTransactionEventPublisher>(publisher);
    }
}
