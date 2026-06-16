using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Infrastructure.Identity;
using CashFlow.Auth.Infrastructure.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Auth.Api.Endpoints;

public static class UserAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapUserAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth/admin")
            .WithTags("User Administration")
            .RequireAuthorization();

        group.MapGet("/users", async (
            InMemoryUserAccountStore userAccountStore,
            CancellationToken cancellationToken) =>
        {
            var users = await userAccountStore.ListAsync(cancellationToken);
            return Results.Ok(users.Select(user => new UserSummary
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                IsActive = user.IsActive,
                AuthenticationSource = "InMemoryFallback"
            }));
        })
        .WithName("ListUsers")
        .WithSummary("Lists locally managed users (development only).");

        group.MapPost("/users", async (
            UserSummary request,
            IUserAdministrationService administrationService,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken) =>
        {
            var user = await administrationService.UpsertUserAsync(request, cancellationToken);
            securityAuditService.Record(new SecurityEventRecord
            {
                EventType = "user.admin.upsert",
                Outcome = "succeeded",
                Subject = user.Email
            });

            return Results.Ok(user);
        })
        .WithName("UpsertUser")
        .WithSummary("Creates or updates a locally managed user.");

        group.MapPost("/users/{email}/disable", async (
            string email,
            IUserAdministrationService administrationService,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken) =>
        {
            await administrationService.DisableUserAsync(email, cancellationToken);
            securityAuditService.Record(new SecurityEventRecord
            {
                EventType = "user.admin.disable",
                Outcome = "succeeded",
                Subject = email
            });

            return Results.NoContent();
        })
        .WithName("DisableUser")
        .WithSummary("Disables a locally managed user.");

        return endpoints;
    }
}
