namespace Aspire.CashFlow.ServiceDefaults.Logging;

public sealed class CloudWatchOptions
{
    public const string SectionName = "CloudWatch";

    public bool Enabled { get; set; }

    public string Region { get; set; } = string.Empty;

    public string ServiceUrl { get; set; } = string.Empty;

    public string LogGroupPrefix { get; set; } = string.Empty;

    public string LogGroupName { get; set; } = string.Empty;

    public int BatchSize { get; set; }

    public int FlushPeriodSeconds { get; set; }

    public string ResolveLogGroupName(string applicationName)
    {
        if (!string.IsNullOrWhiteSpace(LogGroupName))
        {
            return LogGroupName;
        }

        var suffix = applicationName switch
        {
            "CashFlow.Auth.Api" => "auth-api",
            "CashFlow.Transactions.Api" => "transactions-api",
            "CashFlow.Transactions.Relay" => "transactions-relay",
            "CashFlow.Reporting.Api" => "reporting-api",
            "CashFlow.Reporting.Worker" => "reporting-worker",
            "CashFlow.Web" => "web",
            _ => applicationName.Replace('.', '-').ToLowerInvariant(),
        };

        var prefix = LogGroupPrefix.TrimEnd('/');
        return $"{prefix}/{suffix}";
    }
}
