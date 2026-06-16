namespace CashFlow.Web.Services;

public sealed record StoredSession
{
    public string Token { get; init; } = string.Empty;

    public string? RefreshToken { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public bool IsExpired => ExpiresAtUtc <= DateTimeOffset.UtcNow;

    public bool ShouldRefresh(TimeSpan leadTime)
    {
        return !string.IsNullOrWhiteSpace(RefreshToken)
            && ExpiresAtUtc <= DateTimeOffset.UtcNow.Add(leadTime);
    }
}
