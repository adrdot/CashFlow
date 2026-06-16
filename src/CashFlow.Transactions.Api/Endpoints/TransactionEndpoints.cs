using System.Security.Claims;
using Aspire.CashFlow.ServiceDefaults.Authentication;
using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Api.Endpoints;

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        var group = endpoints.MapGroup("/api/transactions").RequireAuthorization();

        group
            .MapPost("/", CreateTransactionAsync)
            .WithName("CreateTransaction")
            .WithSummary("Records a debit or credit transaction for the signed-in user.");

        return endpoints;
    }

    private static async Task<IResult> CreateTransactionAsync(
        CreateTransactionRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ITransactionService transactionService,
        IOptions<TransactionsOptions> transactionsOptions,
        CancellationToken cancellationToken
    )
    {
        var userId = user.ResolveCashFlowUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Problem(
                title: "Authenticated user is required",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var idempotencyKey = httpContext.Request.Headers.TryGetValue(
            "Idempotency-Key",
            out var values
        )
            ? values.FirstOrDefault()
            : null;

        var result = await transactionService.CreateAsync(
            request,
            userId,
            idempotencyKey,
            cancellationToken
        );
        if (result.Succeeded)
        {
            return Results.Ok(result);
        }

        if (
            result.ErrorMessage?.Contains(
                "could not be recorded",
                StringComparison.OrdinalIgnoreCase
            ) == true
        )
        {
            return Results.Problem(
                title: "Transaction persistence failed",
                detail: result.ErrorMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                extensions: new Dictionary<string, object?>
                {
                    ["retryAfterSeconds"] = transactionsOptions.Value.PersistenceRetryAfterSeconds,
                }
            );
        }

        return Results.BadRequest(
            new ProblemDetails
            {
                Title = "Transaction request is invalid",
                Detail = result.ErrorMessage,
                Status = StatusCodes.Status400BadRequest,
            }
        );
    }
}
