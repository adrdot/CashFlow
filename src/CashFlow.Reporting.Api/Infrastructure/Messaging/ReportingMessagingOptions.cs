namespace CashFlow.Reporting.Infrastructure.Messaging;

public sealed class ReportingMessagingOptions
{
    public const string SectionName = "Messaging";

    public string SqsQueueUrl { get; set; } = string.Empty;

    public string DlqQueueUrl { get; set; } = string.Empty;

    public string ServiceUrl { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public int MaxMessages { get; set; } = 10;

    public int WaitTimeSeconds { get; set; } = 20;

    public int VisibilityTimeoutSeconds { get; set; } = 30;
}
