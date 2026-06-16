using System.Net;
using System.Net.Http.Json;
using CashFlow.FunctionalTests.Infrastructure;
using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.ContractTests;
using CashFlow.Reporting.Infrastructure.Persistence;
using CashFlow.Transactions.Application.Contracts;
using CashFlow.Transactions.IntegrationTests;
using CashFlow.Transactions.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.FunctionalTests;

public sealed class CashFlowFunctionalFlowTests : IAsyncLifetime
{
    private const string UserId = TestJwtTokenHelper.DefaultUserId;
    private const decimal CreditAmount = 1000m;
    private const decimal DebitAmount = 400m;

    private readonly DateOnly reportDate = new(2026, 6, 15);

    private TransactionsWebApplicationFactory? transactionsFactory;
    private FunctionalReportingWebApplicationFactory? reportingFactory;

    public async Task InitializeAsync()
    {
        FunctionalTestEnvironment.IsolateFromLocalStack();

        transactionsFactory = new TransactionsWebApplicationFactory(
            TransactionsTestMode.EventStorePersistence
        );
        reportingFactory = new FunctionalReportingWebApplicationFactory();
        await reportingFactory.EnsureDatabaseCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        FunctionalTestEnvironment.Restore();
        if (reportingFactory is not null)
        {
            await reportingFactory.DisposeAsync();
        }

        if (transactionsFactory is not null)
        {
            await transactionsFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreditAndDebit_PostDailyReportAndExports_AreConsistent()
    {
        Assert.NotNull(transactionsFactory);
        Assert.NotNull(reportingFactory);

        using var transactionsClient = transactionsFactory.CreateClient();
        TestJwtTokenHelper.AuthorizeClient(transactionsClient, UserId);

        var credit = await PostTransactionAsync(
            transactionsClient,
            "Credit",
            CreditAmount,
            "Functional flow credit",
            reportDate
        );
        var debit = await PostTransactionAsync(
            transactionsClient,
            "Debit",
            DebitAmount,
            "Functional flow debit",
            reportDate
        );

        await ProjectTransactionAsync(credit);
        await ProjectTransactionAsync(debit);

        using var reportingClient = reportingFactory.CreateClient();
        TestJwtTokenHelper.AuthorizeClient(reportingClient, UserId);

        var reportResponse = await reportingClient.GetAsync(
            $"/api/reports/daily?date={reportDate:yyyy-MM-dd}"
        );
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);

        var report = await reportResponse.Content.ReadFromJsonAsync<DailyReportResult>();
        Assert.NotNull(report);
        Assert.Equal(reportDate, report!.ReportDate);
        Assert.Equal(CreditAmount, report.TotalCredits);
        Assert.Equal(DebitAmount, report.TotalDebits);
        Assert.Equal(CreditAmount - DebitAmount, report.ConsolidatedBalance);
        Assert.Equal(2, report.TransactionVolume);
        Assert.True(report.HasData);

        var csvResponse = await reportingClient.GetAsync(
            $"/api/reports/daily/export/csv?date={reportDate:yyyy-MM-dd}"
        );
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);

        var csvContent = await csvResponse.Content.ReadAsByteArrayAsync();
        ExportAssertions.AssertCsvMatchesReport(report, csvContent);

        var pdfResponse = await reportingClient.GetAsync(
            $"/api/reports/daily/export/pdf?date={reportDate:yyyy-MM-dd}"
        );
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);

        var pdfContent = await pdfResponse.Content.ReadAsByteArrayAsync();
        ExportAssertions.AssertPdfExportIsValid(pdfContent);
    }

    private static async Task<TransactionReceipt> PostTransactionAsync(
        HttpClient client,
        string type,
        decimal amount,
        string description,
        DateOnly transactionDate
    )
    {
        var response = await client.PostAsJsonAsync(
            "/api/transactions",
            new CreateTransactionRequest
            {
                Type = type,
                Amount = amount,
                Description = description,
                TransactionDate = transactionDate,
            }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<CreateTransactionResult>();
        Assert.NotNull(payload);
        Assert.True(payload!.Succeeded);
        Assert.NotNull(payload.Transaction);

        return payload.Transaction!;
    }

    private async Task ProjectTransactionAsync(TransactionReceipt transaction)
    {
        Assert.NotNull(reportingFactory);

        using var scope = reportingFactory.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<TransactionProjectionWriter>();

        await writer.ProjectAsync(
            transaction.Id,
            UserId,
            transaction.Type,
            transaction.Amount,
            transaction.Description,
            transaction.TransactionDate,
            transaction.CreatedAtUtc,
            CancellationToken.None
        );
    }
}
