namespace CashFlow.Reporting.Application.Contracts;

public sealed record DailyReportResult
{
    public DateOnly ReportDate { get; init; }

    public decimal TotalDebits { get; init; }

    public decimal TotalCredits { get; init; }

    public decimal ConsolidatedBalance { get; init; }

    public int TransactionVolume { get; init; }

    public bool HasData { get; init; }

    public bool FromCache { get; init; }

    public DashboardDatasetResult Dataset { get; init; } = new();
}