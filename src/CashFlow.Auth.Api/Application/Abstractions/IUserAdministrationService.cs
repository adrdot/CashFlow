using CashFlow.Auth.Application.Contracts;

namespace CashFlow.Auth.Application.Abstractions;

public interface IUserAdministrationService
{
    Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserSummary> UpsertUserAsync(UserSummary user, CancellationToken cancellationToken = default);

    Task<UserAccessAssignment> AssignAccessAsync(UserAccessAssignment assignment, CancellationToken cancellationToken = default);

    Task DisableUserAsync(string email, CancellationToken cancellationToken = default);
}