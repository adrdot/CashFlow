using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CashFlow.Auth.Infrastructure.Security;

public sealed class PasswordVerifier : IPasswordVerifier
{
    private readonly PasswordHasher<UserAccount> passwordHasher = new();

    public string Hash(UserAccount userAccount, string password)
    {
        return passwordHasher.HashPassword(userAccount, password);
    }

    public bool Verify(UserAccount userAccount, string password)
    {
        var verificationResult = passwordHasher.VerifyHashedPassword(userAccount, userAccount.PasswordHash, password);
        return verificationResult != PasswordVerificationResult.Failed;
    }
}