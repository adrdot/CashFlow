namespace CashFlow.Reporting.Domain.Entities;

public sealed record DailyConsolidatedReport
{
    public DateOnly ReportDate { get; init; }

    public decimal TotalDebits { get; init; }

    public decimal TotalCredits { get; init; }

    public decimal ConsolidatedBalance { get; init; }

    public int TransactionVolume { get; init; }

    public bool HasData { get; init; }

    public DashboardDataset Dataset { get; init; } = new();
}
