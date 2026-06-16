namespace CashFlow.Web.Configuration;

public sealed class AuthSessionOptions
{
    public const string SectionName = "Session";

    /// <summary>
    /// Minutes before access-token expiry to proactively refresh via the auth API.
    /// </summary>
    public int RefreshLeadTimeMinutes { get; init; }
}
