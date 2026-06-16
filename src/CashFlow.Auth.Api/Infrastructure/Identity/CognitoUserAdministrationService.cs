using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Domain.Entities;

namespace CashFlow.Auth.Infrastructure.Identity;

/// <summary>
/// Scaffold for Cognito user administration. Full implementation is deferred — see
/// <c>specs/005-adicionar-seguran-front/deferred.md</c>.
/// When Cognito is enabled, this service is registered but does not call Cognito Admin APIs yet.
/// </summary>
public sealed class CognitoUserAdministrationService(IUserAccountStore userAccountStore) : IUserAdministrationService
{
    public async Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var userAccount = await userAccountStore.FindByEmailAsync(email, cancellationToken);
        return userAccount is null ? null : Map(userAccount);
    }

    public Task<UserSummary> UpsertUserAsync(UserSummary user, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(user);
    }

    public Task<UserAccessAssignment> AssignAccessAsync(UserAccessAssignment assignment, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(assignment);
    }

    public Task DisableUserAsync(string email, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static UserSummary Map(UserAccount userAccount)
    {
        return new UserSummary
        {
            Id = userAccount.Id,
            Email = userAccount.Email,
            DisplayName = userAccount.DisplayName,
            IsActive = userAccount.IsActive,
            AuthenticationSource = "InMemoryFallback"
        };
    }
}