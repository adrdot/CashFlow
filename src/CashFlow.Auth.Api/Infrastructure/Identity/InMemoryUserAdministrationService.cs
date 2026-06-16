using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Domain.Entities;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Identity.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.Auth.Infrastructure.Identity;

public sealed class InMemoryUserAdministrationService(
    InMemoryUserAccountStore userAccountStore,
    IOptions<LocalAuthOptions> localAuthOptions
) : IUserAdministrationService
{
    public async Task<UserSummary?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var userAccount = await userAccountStore.FindByEmailAsync(email, cancellationToken);
        return userAccount is null ? null : Map(userAccount);
    }

    public async Task<UserSummary> UpsertUserAsync(
        UserSummary user,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await userAccountStore.FindByEmailAsync(user.Email, cancellationToken);
        var userAccount = new UserAccount
        {
            Id = existing?.Id ?? (user.Id == Guid.Empty ? Guid.NewGuid() : user.Id),
            Email = user.Email,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
        };

        var storedUser = await userAccountStore.UpsertAsync(
            userAccount,
            password: existing is null ? localAuthOptions.Value.DefaultUserPassword : null,
            cancellationToken
        );

        return new UserSummary
        {
            Id = storedUser.Id,
            Email = storedUser.Email,
            DisplayName = storedUser.DisplayName,
            IsActive = storedUser.IsActive,
            MfaEnabled = user.MfaEnabled,
            Groups = user.Groups,
            AuthenticationSource = "InMemoryFallback",
        };
    }

    public Task<UserAccessAssignment> AssignAccessAsync(
        UserAccessAssignment assignment,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(assignment);
    }

    public async Task DisableUserAsync(string email, CancellationToken cancellationToken = default)
    {
        await userAccountStore.SetActiveAsync(email, isActive: false, cancellationToken);
    }

    private static UserSummary Map(UserAccount userAccount)
    {
        return new UserSummary
        {
            Id = userAccount.Id,
            Email = userAccount.Email,
            DisplayName = userAccount.DisplayName,
            IsActive = userAccount.IsActive,
            AuthenticationSource = "InMemoryFallback",
        };
    }
}
