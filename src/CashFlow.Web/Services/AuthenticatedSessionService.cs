using CashFlow.Web.Configuration;
using Microsoft.Extensions.Options;

namespace CashFlow.Web.Services;

public sealed class AuthenticatedSessionService(
    SessionStore sessionStore,
    AuthApiClient authApiClient,
    IOptions<AuthSessionOptions> sessionOptions
)
{
    private TimeSpan RefreshLeadTime =>
        TimeSpan.FromMinutes(Math.Max(1, sessionOptions.Value.RefreshLeadTimeMinutes));

    public async Task<StoredSession?> RestoreSessionAsync()
    {
        var storedSession = await sessionStore.LoadAsync();
        if (storedSession is null)
        {
            return null;
        }

        if (!storedSession.IsExpired && !storedSession.ShouldRefresh(RefreshLeadTime))
        {
            return storedSession;
        }

        if (storedSession.IsExpired)
        {
            return await RestoreExpiredSessionAsync(storedSession);
        }

        var refreshedSession = await TryRefreshAsync(storedSession);
        if (refreshedSession is not null && !refreshedSession.IsExpired)
        {
            return refreshedSession;
        }

        return storedSession;
    }

    private async Task<StoredSession?> RestoreExpiredSessionAsync(StoredSession storedSession)
    {
        if (string.IsNullOrWhiteSpace(storedSession.RefreshToken))
        {
            await sessionStore.ClearAsync();
            return null;
        }

        var refreshedSession = await TryRefreshAsync(storedSession);
        if (refreshedSession is null || refreshedSession.IsExpired)
        {
            await sessionStore.ClearAsync();
            return null;
        }

        return refreshedSession;
    }

    private async Task<StoredSession?> TryRefreshAsync(StoredSession storedSession)
    {
        if (string.IsNullOrWhiteSpace(storedSession.RefreshToken))
        {
            return null;
        }

        var result = await authApiClient.RefreshSessionAsync(storedSession.RefreshToken);
        if (!result.Succeeded || result.Session is null || string.IsNullOrWhiteSpace(result.Token))
        {
            return null;
        }

        var refreshedSession = storedSession with
        {
            Token = result.Token,
            RefreshToken = result.RefreshToken ?? storedSession.RefreshToken,
            Email = result.Session.Email,
            DisplayName = result.Session.DisplayName,
            ExpiresAtUtc = result.Session.ExpiresAtUtc,
        };

        await sessionStore.SaveAsync(refreshedSession);
        return refreshedSession;
    }
}
