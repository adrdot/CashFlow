namespace CashFlow.Transactions.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public string SnsTopicArn { get; set; } = string.Empty;

    public string SqsQueueUrl { get; set; } = string.Empty;

    public string DlqQueueUrl { get; set; } = string.Empty;

    public string ServiceUrl { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public int MaxPublishRetries { get; set; }

    public bool Enabled { get; set; }
}
