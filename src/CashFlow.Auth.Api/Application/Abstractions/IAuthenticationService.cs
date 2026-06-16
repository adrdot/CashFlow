using CashFlow.Auth.Application.Contracts;

namespace CashFlow.Auth.Application.Abstractions;

public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(
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

    Task LogoutAsync(string token, CancellationToken cancellationToken = default);
}
