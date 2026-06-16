using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Application.Contracts;

namespace CashFlow.Auth.Application.UseCases;

public sealed class LoginUserHandler(IIdentityProvider identityProvider) : IAuthenticationService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        return await identityProvider.AuthenticateAsync(request, cancellationToken);
    }

    public Task<SessionState?> ValidateSessionAsync(string token, CancellationToken cancellationToken = default)
    {
        return identityProvider.ValidateSessionAsync(token, cancellationToken);
    }

    public Task LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        return identityProvider.RevokeSessionAsync(token, cancellationToken);
    }

    public Task<LoginResult> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return identityProvider.RefreshSessionAsync(refreshToken, cancellationToken);
    }
}