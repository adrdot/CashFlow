using System.Net.Http.Headers;
using System.Net.Http.Json;
using CashFlow.Transactions.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Web.Services;

public sealed class TransactionsApiClient(HttpClient httpClient)
{
    public async Task<CreateTransactionResult> CreateAsync(
        CreateTransactionRequest request,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/")
            {
                Content = JsonContent.Create(request),
            };

            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CreateTransactionResult>(
                        cancellationToken: cancellationToken
                    )
                    ?? CreateTransactionResult.Failure(
                        "Transaction service returned an empty response."
                    );
            }

            ProblemDetails? problem = null;
            if (response.Content.Headers.ContentLength > 0)
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
                    cancellationToken: cancellationToken
                );
            }
            return CreateTransactionResult.Failure(
                problem?.Detail
                    ?? $"Transaction could not be recorded. Status: {response.StatusCode}"
            );
        }
        catch (HttpRequestException)
        {
            return CreateTransactionResult.Failure(
                "Transaction service is unavailable. Try again later."
            );
        }
    }
}
