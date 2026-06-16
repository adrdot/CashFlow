namespace CashFlow.Auth.Application.Contracts;

public sealed class SecurityEventRecord
{
    public string EventType { get; init; } = string.Empty;

    public string Outcome { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyDictionary<string, string> Dimensions { get; init; } = new Dictionary<string, string>();
}