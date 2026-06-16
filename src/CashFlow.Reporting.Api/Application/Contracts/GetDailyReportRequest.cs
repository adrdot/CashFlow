using System.ComponentModel.DataAnnotations;

namespace CashFlow.Reporting.Application.Contracts;

public sealed record GetDailyReportRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    public DateOnly? ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}
