namespace CashFlow.Reporting.Infrastructure.Caching;

public sealed class ReportingCacheOptions
{
    public const string SectionName = "Reporting:Cache";

    public int CurrentDayTtlMinutes { get; set; } = 15;

    public int ClosedDayTtlHours { get; set; } = 24;
}

public sealed class ReportingRedisOptions
{
    public const string SectionName = "Reporting:Redis";

    public bool Enabled { get; set; }

    public string Configuration { get; set; } = string.Empty;
}
