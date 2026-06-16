using CashFlow.Auth.Domain.Entities;

namespace CashFlow.Auth.Application.Abstractions;

public interface IPasswordVerifier
{
    string Hash(UserAccount userAccount, string password);

    bool Verify(UserAccount userAccount, string password);
}