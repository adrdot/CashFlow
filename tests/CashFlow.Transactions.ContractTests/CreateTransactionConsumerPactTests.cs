using System.Net;
using System.Text.RegularExpressions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.ContractTests.Infrastructure;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Output.Xunit;
using Xunit.Abstractions;
using PactMatch = PactNet.Matchers.Match;

namespace CashFlow.Transactions.ContractTests;

[Collection("PactConsumer")]
public sealed class CreateTransactionConsumerPactTests
{
    private const string BearerTokenExample =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.contract-test-token";

    private readonly IPactBuilderV4 pactBuilder;

    public CreateTransactionConsumerPactTests(ITestOutputHelper output)
    {
        Directory.CreateDirectory(PactConstants.PactDirectory);

        var pact = Pact.V4(
            PactConstants.ConsumerName,
            PactConstants.ProviderName,
            new PactConfig
            {
                PactDir = PactConstants.PactDirectory,
                Outputters = [new XunitOutput(output)],
            }
        );

        pactBuilder = pact.WithHttpInteractions();
    }

    [Fact]
    public async Task CreateTransaction_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        pactBuilder
            .UponReceiving("a create transaction request without bearer token")
            .Given("no bearer token is provided")
            .WithRequest(HttpMethod.Post, "/api/transactions/")
            .WithJsonBody(ValidRequestBody())
            .WillRespond()
            .WithStatus(HttpStatusCode.Unauthorized);

        await pactBuilder.VerifyAsync(async context =>
        {
            var client = new TransactionsApiConsumerClient(
                new HttpClient { BaseAddress = context.MockServerUri }
            );
            using var response = await client.CreateTransactionAsync(ValidRequest());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        });
    }

    [Fact]
    public async Task CreateTransaction_ReturnsBadRequest_WhenPayloadIsInvalid()
    {
        var invalidRequestBody = new
        {
            type = "Transfer",
            amount = 0m,
            description = "ab",
            transactionDate = (string?)null,
        };

        pactBuilder
            .UponReceiving("a create transaction request with invalid payload")
            .Given("a user is authenticated")
            .WithRequest(HttpMethod.Post, "/api/transactions/")
            .WithHeader("Authorization", PactMatch.Regex(BearerTokenExample, "Bearer .+"))
            .WithJsonBody(invalidRequestBody)
            .WillRespond()
            .WithStatus(HttpStatusCode.BadRequest)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new { title = "Transaction request is invalid", status = 400 });

        await pactBuilder.VerifyAsync(async context =>
        {
            var client = new TransactionsApiConsumerClient(
                new HttpClient { BaseAddress = context.MockServerUri }
            );
            using var response = await client.CreateTransactionAsync(
                new CreateTransactionRequest
                {
                    Type = "Transfer",
                    Amount = 0m,
                    Description = "ab",
                    TransactionDate = null,
                },
                BearerTokenExample
            );
            var problem = await client.ReadProblemDetailsAsync(response);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Transaction request is invalid", problem?.Title);
        });
    }

    [Fact]
    public async Task CreateTransaction_ReturnsReceiptContract_WhenRequestIsValid()
    {
        pactBuilder
            .UponReceiving("a valid create transaction request")
            .Given("a user is authenticated")
            .WithRequest(HttpMethod.Post, "/api/transactions/")
            .WithHeader("Authorization", PactMatch.Regex(BearerTokenExample, "Bearer .+"))
            .WithJsonBody(ValidRequestBody())
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(
                new
                {
                    succeeded = true,
                    errorMessage = (string?)null,
                    transaction = new
                    {
                        id = PactMatch.Regex(
                            "11111111-1111-1111-1111-111111111111",
                            "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
                        ),
                        type = "Debit",
                        amount = 150.75m,
                        description = "Contract test transaction",
                        transactionDate = "2026-06-14",
                        createdAtUtc = PactMatch.Regex(
                            "2026-06-14T12:00:00.0000000+00:00",
                            "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(\\.\\d+)?(Z|[+-]\\d{2}:\\d{2})$"
                        ),
                    },
                }
            );

        await pactBuilder.VerifyAsync(async context =>
        {
            var client = new TransactionsApiConsumerClient(
                new HttpClient { BaseAddress = context.MockServerUri }
            );
            using var response = await client.CreateTransactionAsync(
                ValidRequest(),
                BearerTokenExample
            );
            var payload = await client.ReadSuccessPayloadAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(payload);
            Assert.True(payload.Succeeded);
            Assert.NotNull(payload.Transaction);
            Assert.Equal("Debit", payload.Transaction.Type);
            Assert.Equal(150.75m, payload.Transaction.Amount);
            Assert.Matches(new Regex("^[0-9a-fA-F-]{36}$"), payload.Transaction.Id.ToString());
        });
    }

    [Fact]
    public async Task CreateTransaction_ReturnsServiceUnavailable_WhenPersistenceFails()
    {
        pactBuilder
            .UponReceiving("a create transaction request when persistence fails")
            .Given("transaction persistence fails")
            .WithRequest(HttpMethod.Post, "/api/transactions/")
            .WithHeader("Authorization", PactMatch.Regex(BearerTokenExample, "Bearer .+"))
            .WithJsonBody(ValidRequestBody())
            .WillRespond()
            .WithStatus(HttpStatusCode.ServiceUnavailable)
            .WithHeader("Content-Type", "application/problem+json")
            .WithJsonBody(
                new
                {
                    title = "Transaction persistence failed",
                    retryAfterSeconds = PactMatch.Type(5),
                }
            );

        await pactBuilder.VerifyAsync(async context =>
        {
            var client = new TransactionsApiConsumerClient(
                new HttpClient { BaseAddress = context.MockServerUri }
            );
            using var response = await client.CreateTransactionAsync(
                ValidRequest(),
                BearerTokenExample
            );
            var problem = await client.ReadProblemDetailsAsync(response);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("Transaction persistence failed", problem?.Title);
        });
    }

    private static CreateTransactionRequest ValidRequest() =>
        new()
        {
            Type = "Debit",
            Amount = 150.75m,
            Description = "Contract test transaction",
            TransactionDate = new DateOnly(2026, 6, 14),
        };

    private static object ValidRequestBody() =>
        new
        {
            type = "Debit",
            amount = 150.75m,
            description = "Contract test transaction",
            transactionDate = "2026-06-14",
        };
}

[CollectionDefinition("PactConsumer", DisableParallelization = true)]
public sealed class PactConsumerCollection;
