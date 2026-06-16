using CashFlow.Auth.Domain.Entities;

namespace CashFlow.Auth.Infrastructure.Persistence.Abstractions;

public interface IUserAccountStore
{
    Task<UserAccount?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    );
}
