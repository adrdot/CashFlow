namespace CashFlow.Auth.Infrastructure.Observability;

public sealed class AuthLogEvents
{
    public const int AuthenticationSucceeded = 1000;

    public const int AuthenticationFailed = 1001;

    public const int SessionRevoked = 1002;
}
