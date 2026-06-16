using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Domain.Entities;

namespace CashFlow.Reporting.Application;

public static class DailyReportBuilder
{
    public static DailyReportResult Build(DateOnly reportDate, DailySummary? summary)
    {
        if (summary is null || !summary.HasData)
        {
            return CreateZeroState(reportDate);
        }

        var balance = summary.ConsolidatedBalance;

        return new DailyReportResult
        {
            ReportDate = reportDate,
            TotalDebits = summary.TotalDebits,
            TotalCredits = summary.TotalCredits,
            ConsolidatedBalance = balance,
            TransactionVolume = summary.TransactionVolume,
            HasData = true,
            FromCache = false,
            Dataset = new DashboardDatasetResult
            {
                LineSeries =
                [
                    new ChartMetricResult { Label = "Debits", Value = summary.TotalDebits },
                    new ChartMetricResult { Label = "Credits", Value = summary.TotalCredits },
                    new ChartMetricResult { Label = "Balance", Value = balance }
                ],
                BarSeries =
                [
                    new ChartMetricResult { Label = "Debit Entries", Value = summary.DebitEntryCount },
                    new ChartMetricResult { Label = "Credit Entries", Value = summary.CreditEntryCount },
                    new ChartMetricResult { Label = "Total Entries", Value = summary.TransactionVolume }
                ],
                PieSeries =
                [
                    new ChartMetricResult { Label = "Debits", Value = summary.TotalDebits },
                    new ChartMetricResult { Label = "Credits", Value = summary.TotalCredits }
                ]
            }
        };
    }

    private static DailyReportResult CreateZeroState(DateOnly reportDate) => new()
    {
        ReportDate = reportDate,
        TotalDebits = 0m,
        TotalCredits = 0m,
        ConsolidatedBalance = 0m,
        TransactionVolume = 0,
        HasData = false,
        FromCache = false,
        Dataset = new DashboardDatasetResult
        {
            LineSeries =
            [
                new ChartMetricResult { Label = "Debits", Value = 0m },
                new ChartMetricResult { Label = "Credits", Value = 0m },
                new ChartMetricResult { Label = "Balance", Value = 0m }
            ],
            BarSeries =
            [
                new ChartMetricResult { Label = "Debit Entries", Value = 0m },
                new ChartMetricResult { Label = "Credit Entries", Value = 0m },
                new ChartMetricResult { Label = "Total Entries", Value = 0m }
            ],
            PieSeries =
            [
                new ChartMetricResult { Label = "Debits", Value = 0m },
                new ChartMetricResult { Label = "Credits", Value = 0m }
            ]
        }
    };
}
