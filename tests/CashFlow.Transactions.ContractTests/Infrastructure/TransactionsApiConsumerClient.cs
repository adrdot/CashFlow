using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.Transactions.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Transactions.ContractTests.Infrastructure;

internal sealed class TransactionsApiConsumerClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<HttpResponseMessage> CreateTransactionAsync(
        CreateTransactionRequest request,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/")
        {
            Content = JsonContent.Create(request, options: WebJsonOptions)
        };

        if (bearerToken is not null)
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return await httpClient.SendAsync(httpRequest, cancellationToken);
    }

    public async Task<CreateTransactionResult?> ReadSuccessPayloadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        return await response.Content.ReadFromJsonAsync<CreateTransactionResult>(cancellationToken: cancellationToken);
    }

    public async Task<ProblemDetails?> ReadProblemDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        return await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
    }
}
