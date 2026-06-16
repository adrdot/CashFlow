using CashFlow.Auth.Application.Contracts;

namespace CashFlow.Auth.Infrastructure.OAuth;

public sealed class DevAuthorizationCodeStore
{
    private readonly Dictionary<string, PendingAuthorizationCode> codes = new(
        StringComparer.Ordinal
    );
    private readonly object gate = new();

    public string Create(
        LoginResult loginResult,
        string redirectUri,
        string clientId,
        string state,
        TimeSpan ttl
    )
    {
        var code = Convert
            .ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var pending = new PendingAuthorizationCode(
            loginResult,
            redirectUri,
            clientId,
            state,
            DateTimeOffset.UtcNow.Add(ttl)
        );

        lock (gate)
        {
            PurgeExpired_NoLock();
            codes[code] = pending;
        }

        return code;
    }

    public bool TryRedeem(
        string code,
        string redirectUri,
        string clientId,
        out LoginResult? loginResult
    )
    {
        loginResult = null;

        lock (gate)
        {
            PurgeExpired_NoLock();

            if (!codes.TryGetValue(code, out var pending))
            {
                return false;
            }

            codes.Remove(code);

            if (
                pending.IsExpired
                || !string.Equals(pending.RedirectUri, redirectUri, StringComparison.Ordinal)
                || !string.Equals(pending.ClientId, clientId, StringComparison.Ordinal)
            )
            {
                return false;
            }

            loginResult = pending.LoginResult;
            return loginResult.Succeeded;
        }
    }

    private void PurgeExpired_NoLock()
    {
        var expiredKeys = codes
            .Where(entry => entry.Value.IsExpired)
            .Select(entry => entry.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            codes.Remove(key);
        }
    }

    private sealed record PendingAuthorizationCode(
        LoginResult LoginResult,
        string RedirectUri,
        string ClientId,
        string State,
        DateTimeOffset ExpiresAtUtc
    )
    {
        public bool IsExpired => ExpiresAtUtc <= DateTimeOffset.UtcNow;
    }
}
