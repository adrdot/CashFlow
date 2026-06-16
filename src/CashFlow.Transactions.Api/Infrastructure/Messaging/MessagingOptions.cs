namespace CashFlow.Transactions.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public string SnsTopicArn { get; set; } = string.Empty;

    public string SqsQueueUrl { get; set; } = string.Empty;

    public string DlqQueueUrl { get; set; } = string.Empty;

    public string ServiceUrl { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public int MaxPublishRetries { get; set; } = 5;

    public bool Enabled { get; set; } = true;
}
