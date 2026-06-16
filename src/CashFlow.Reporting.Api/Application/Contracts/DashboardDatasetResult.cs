namespace CashFlow.Reporting.Application.Contracts;

public sealed record DashboardDatasetResult
{
    public IReadOnlyList<ChartMetricResult> LineSeries { get; init; } = [];

    public IReadOnlyList<ChartMetricResult> BarSeries { get; init; } = [];

    public IReadOnlyList<ChartMetricResult> PieSeries { get; init; } = [];
}