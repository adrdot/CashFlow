using CashFlow.Auth.Domain.Entities;

namespace CashFlow.Auth.Application.Abstractions;

public interface IUserAccountStore
{
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}