namespace CashFlow.Reporting.Infrastructure.Messaging;

public sealed class ReportingMessagingOptions
{
    public const string SectionName = "Messaging";

    public string SqsQueueUrl { get; set; } = string.Empty;

    public string DlqQueueUrl { get; set; } = string.Empty;

    public string ServiceUrl { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public int MaxMessages { get; set; }

    public int WaitTimeSeconds { get; set; }

    public int VisibilityTimeoutSeconds { get; set; }

    public int QueueMissingRetrySeconds { get; set; }

    public int ErrorRetrySeconds { get; set; }
}
