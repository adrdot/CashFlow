namespace CashFlow.Auth.Infrastructure.Identity;

public sealed class LocalMfaChallengeStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, LocalMfaChallenge> challenges = new(StringComparer.Ordinal);

    public string CreateChallenge(string email, TimeSpan ttl, CognitoAuthResult? pendingCognitoAuth = null)
    {
        var session = Guid.NewGuid().ToString("N");
        var challenge = new LocalMfaChallenge(email, DateTimeOffset.UtcNow.Add(ttl), pendingCognitoAuth);

        lock (sync)
        {
            challenges[session] = challenge;
        }

        return session;
    }

    public bool IsLocalChallenge(string session)
    {
        lock (sync)
        {
            return challenges.ContainsKey(session);
        }
    }

    public bool Validate(string session, string email, string mfaCode, string expectedCode)
    {
        lock (sync)
        {
            if (!challenges.TryGetValue(session, out var challenge))
            {
                return false;
            }

            challenges.Remove(session);

            return string.Equals(challenge.Email, email, StringComparison.OrdinalIgnoreCase)
                && challenge.ExpiresAtUtc >= DateTimeOffset.UtcNow
                && string.Equals(mfaCode, expectedCode, StringComparison.Ordinal);
        }
    }

    public bool TryCompleteWithPendingCognitoAuth(
        string session,
        string email,
        string mfaCode,
        string expectedCode,
        out CognitoAuthResult? pendingCognitoAuth)
    {
        pendingCognitoAuth = null;

        lock (sync)
        {
            if (!challenges.TryGetValue(session, out var challenge))
            {
                return false;
            }

            var isValid = string.Equals(challenge.Email, email, StringComparison.OrdinalIgnoreCase)
                && challenge.ExpiresAtUtc >= DateTimeOffset.UtcNow
                && string.Equals(mfaCode, expectedCode, StringComparison.Ordinal);

            challenges.Remove(session);

            if (!isValid || challenge.PendingCognitoAuth is null)
            {
                return false;
            }

            pendingCognitoAuth = challenge.PendingCognitoAuth;
            return true;
        }
    }

    private sealed record LocalMfaChallenge(
        string Email,
        DateTimeOffset ExpiresAtUtc,
        CognitoAuthResult? PendingCognitoAuth = null);
}
