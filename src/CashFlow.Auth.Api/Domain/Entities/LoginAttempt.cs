namespace CashFlow.Auth.Domain.Entities;

public sealed class LoginAttempt
{
    public string Email { get; init; } = string.Empty;

    public DateTimeOffset AttemptedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool Succeeded { get; init; }

    public string FailureReason { get; init; } = string.Empty;
}