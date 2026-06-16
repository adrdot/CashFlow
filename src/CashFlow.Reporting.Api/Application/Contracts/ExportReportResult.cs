namespace CashFlow.Reporting.Application.Contracts;

public sealed record ExportReportResult
{
    public required byte[] Content { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }
}
