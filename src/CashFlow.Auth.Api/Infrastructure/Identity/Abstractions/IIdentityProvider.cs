using CashFlow.Auth.Application.Contracts;

namespace CashFlow.Auth.Infrastructure.Identity.Abstractions;

public interface IIdentityProvider
{
    Task<LoginResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    );

    Task<SessionState?> ValidateSessionAsync(
        string token,
        CancellationToken cancellationToken = default
    );

    Task<LoginResult> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    );

    Task RevokeSessionAsync(string token, CancellationToken cancellationToken = default);
}
