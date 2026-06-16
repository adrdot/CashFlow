using System.Globalization;
using System.Text;
using CashFlow.Reporting.Application.Contracts;

namespace CashFlow.FunctionalTests.Infrastructure;

internal static class ExportAssertions
{
    public static void AssertCsvMatchesReport(DailyReportResult report, byte[] csvContent)
    {
        var text = Encoding.UTF8.GetString(csvContent);
        var lines = text.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        Assert.True(lines.Length >= 2, "CSV export must contain a header row and a totals row.");

        var header = lines[0];
        Assert.Contains("TotalDebits", header, StringComparison.Ordinal);
        Assert.Contains("TotalCredits", header, StringComparison.Ordinal);

        var totals = lines[1].Split(',');
        Assert.Equal(5, totals.Length);

        Assert.Equal(report.ReportDate, DateOnly.Parse(totals[0], CultureInfo.InvariantCulture));
        Assert.Equal(report.TotalDebits, decimal.Parse(totals[1], CultureInfo.InvariantCulture));
        Assert.Equal(report.TotalCredits, decimal.Parse(totals[2], CultureInfo.InvariantCulture));
        Assert.Equal(
            report.ConsolidatedBalance,
            decimal.Parse(totals[3], CultureInfo.InvariantCulture)
        );
        Assert.Equal(report.TransactionVolume, int.Parse(totals[4], CultureInfo.InvariantCulture));
    }

    public static void AssertPdfExportIsValid(byte[] pdfContent)
    {
        Assert.True(pdfContent.Length > 100, "PDF export should not be empty.");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdfContent, 0, 4));
    }
}
