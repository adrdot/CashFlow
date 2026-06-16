namespace CashFlow.Auth.Domain.Entities;

public sealed class SecurityFinding
{
    public string Source { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateTimeOffset DetectedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}