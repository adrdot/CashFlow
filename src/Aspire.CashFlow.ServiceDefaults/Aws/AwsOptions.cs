namespace Aspire.CashFlow.ServiceDefaults.Aws;

public sealed class AwsOptions
{
    public const string SectionName = "AWS";

    public string Region { get; set; } = string.Empty;

    public string ServiceUrl { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string LocalStackAccountId { get; set; } = string.Empty;
}
