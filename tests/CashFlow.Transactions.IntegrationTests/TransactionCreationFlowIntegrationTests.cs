using System.Net;
using System.Net.Http.Json;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.IntegrationTests.Infrastructure;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class TransactionCreationFlowIntegrationTests : IAsyncLifetime
{
    private TransactionsWebApplicationFactory? factory;

    public Task InitializeAsync()
    {
        factory = new TransactionsWebApplicationFactory(TransactionsTestMode.EventStorePersistence);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateTransaction_PersistsEventStore_WhenRequestIsValid()
    {
        if (factory is null)
        {
            return;
        }

        using var client = factory.CreateClient();
        TestJwtTokenHelper.AuthorizeClient(client);

        var response = await client.PostAsJsonAsync(
            "/api/transactions",
            new CreateTransactionRequest
            {
                Type = "Credit",
                Amount = 250.10m,
                Description = "Integration flow credit",
                TransactionDate = new DateOnly(2026, 6, 14),
            }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CreateTransactionResult>();
        Assert.NotNull(payload);
        Assert.True(payload.Succeeded);
        Assert.NotNull(payload.Transaction);

        Assert.NotNull(factory.EventStore);
        Assert.Single(factory.EventStore.AppendedEvents);
        Assert.Equal(
            payload.Transaction!.Id,
            factory.EventStore.AppendedEvents[0].Event.TransactionId
        );
    }

    [Fact]
    public async Task CreateTransaction_ReturnsSameTransaction_WhenIdempotencyKeyIsReplayed()
    {
        if (factory is null)
        {
            return;
        }

        using var client = factory.CreateClient();
        TestJwtTokenHelper.AuthorizeClient(client);

        const string idempotencyKey = "integration-idem-key-001";
        var request = new CreateTransactionRequest
        {
            Type = "Debit",
            Amount = 42.50m,
            Description = "Idempotent debit",
            TransactionDate = new DateOnly(2026, 6, 14),
        };

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
        {
            Content = JsonContent.Create(request),
        };
        firstRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<CreateTransactionResult>();
        Assert.NotNull(firstPayload);
        Assert.True(firstPayload.Succeeded);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
        {
            Content = JsonContent.Create(
                new CreateTransactionRequest
                {
                    Type = "Credit",
                    Amount = 999m,
                    Description = "Different body should be ignored",
                    TransactionDate = new DateOnly(2026, 1, 1),
                }
            ),
        };
        secondRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var secondResponse = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondPayload =
            await secondResponse.Content.ReadFromJsonAsync<CreateTransactionResult>();
        Assert.NotNull(secondPayload);
        Assert.True(secondPayload.Succeeded);
        Assert.Equal(firstPayload.Transaction!.Id, secondPayload.Transaction!.Id);
        Assert.Equal(firstPayload.Transaction.Type, secondPayload.Transaction.Type);
        Assert.Equal(firstPayload.Transaction.Amount, secondPayload.Transaction.Amount);

        Assert.NotNull(factory.EventStore);
        Assert.Single(factory.EventStore.AppendedEvents);
    }

    [Fact]
    public async Task CreateTransaction_ReturnsServiceUnavailable_WhenEventStoreFails()
    {
        await using var failingFactory = new TransactionsWebApplicationFactory(
            TransactionsTestMode.FailingEventStore
        );

        using var client = failingFactory.CreateClient();
        TestJwtTokenHelper.AuthorizeClient(client);

        var response = await client.PostAsJsonAsync(
            "/api/transactions",
            new CreateTransactionRequest
            {
                Type = "Debit",
                Amount = 10m,
                Description = "EventStore failure path",
                TransactionDate = new DateOnly(2026, 6, 14),
            }
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
