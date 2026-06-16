using System.Collections.Concurrent;
using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Domain.Entities;
using CashFlow.Auth.Infrastructure.Security;

namespace CashFlow.Auth.Infrastructure.Identity;

public sealed class InMemoryUserAccountStore : IUserAccountStore
{
    private readonly IPasswordVerifier passwordVerifier;
    private readonly ConcurrentDictionary<string, UserAccount> userAccounts;

    public InMemoryUserAccountStore(IPasswordVerifier passwordVerifier)
    {
        this.passwordVerifier = passwordVerifier;
        userAccounts = new ConcurrentDictionary<string, UserAccount>(StringComparer.OrdinalIgnoreCase);
        SeedDefaultAdmin();
    }

    public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        userAccounts.TryGetValue(email, out var userAccount);
        return Task.FromResult(userAccount);
    }

    public Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UserAccount> users = userAccounts.Values.OrderBy(user => user.Email).ToList();
        return Task.FromResult(users);
    }

    public Task<UserAccount> UpsertAsync(UserAccount userAccount, string? password, CancellationToken cancellationToken = default)
    {
        var existing = userAccounts.GetValueOrDefault(userAccount.Email);
        var passwordHash = string.IsNullOrWhiteSpace(password)
            ? existing?.PasswordHash ?? string.Empty
            : passwordVerifier.Hash(userAccount, password);

        var storedUser = new UserAccount
        {
            Id = userAccount.Id,
            Email = userAccount.Email,
            DisplayName = userAccount.DisplayName,
            IsActive = userAccount.IsActive,
            PasswordHash = passwordHash
        };

        userAccounts[storedUser.Email] = storedUser;
        return Task.FromResult(storedUser);
    }

    public Task<UserAccount?> SetActiveAsync(string email, bool isActive, CancellationToken cancellationToken = default)
    {
        if (!userAccounts.TryGetValue(email, out var existing))
        {
            return Task.FromResult<UserAccount?>(null);
        }

        var updated = new UserAccount
        {
            Id = existing.Id,
            Email = existing.Email,
            DisplayName = existing.DisplayName,
            IsActive = isActive,
            PasswordHash = existing.PasswordHash
        };

        userAccounts[email] = updated;
        return Task.FromResult<UserAccount?>(updated);
    }

    private void SeedDefaultAdmin()
    {
        var seededAdmin = new UserAccount
        {
            Id = Guid.Parse("b2f02e39-71d0-4d73-96df-f8626776f2a4"),
            Email = "admin@cashflow.local",
            DisplayName = "Cash Flow Admin",
            IsActive = true
        };

        userAccounts[seededAdmin.Email] = new UserAccount
        {
            Id = seededAdmin.Id,
            Email = seededAdmin.Email,
            DisplayName = seededAdmin.DisplayName,
            IsActive = seededAdmin.IsActive,
            PasswordHash = passwordVerifier.Hash(seededAdmin, "Pass@word1")
        };
    }
}
