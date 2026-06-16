namespace CashFlow.Reporting.Domain.Entities;

public sealed record DashboardDataset
{
    public IReadOnlyList<DashboardMetric> LineSeries { get; init; } = [];

    public IReadOnlyList<DashboardMetric> BarSeries { get; init; } = [];

    public IReadOnlyList<DashboardMetric> PieSeries { get; init; } = [];
}

public sealed record DashboardMetric
{
    public string Label { get; init; } = string.Empty;

    public decimal Value { get; init; }
}
