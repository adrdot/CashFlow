using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Domain.Entities;

namespace CashFlow.Auth.Application.Abstractions;

public interface ITokenService
{
    string CreateToken(UserAccount userAccount);

    string CreateRefreshToken(UserAccount userAccount);

    SessionState? ValidateToken(string token);

    string? ValidateRefreshToken(string refreshToken);
}