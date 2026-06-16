namespace CashFlow.Reporting.Application.Contracts;

public sealed record ChartMetricResult
{
    public string Label { get; init; } = string.Empty;

    public decimal Value { get; init; }
}
