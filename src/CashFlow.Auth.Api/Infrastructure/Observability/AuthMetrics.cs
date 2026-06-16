namespace CashFlow.Auth.Infrastructure.Observability;

public sealed class AuthMetrics
{
    public const string LoginSucceeded = "auth.login.succeeded";

    public const string LoginFailed = "auth.login.failed";

    public const string SessionValidated = "auth.session.validated";

    public const string TokenRefreshSucceeded = "auth.token.refresh.succeeded";

    public const string TokenRefreshFailed = "auth.token.refresh.failed";
}
