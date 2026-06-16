using System.Security.Claims;
using Aspire.CashFlow.ServiceDefaults.Authentication;
using CashFlow.Reporting.Application.Abstractions;
using CashFlow.Reporting.Application.Contracts;
using CashFlow.Reporting.Infrastructure.Exports.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Reporting.Api.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reports").RequireAuthorization();

        group
            .MapGet("/daily", GetDailyReportAsync)
            .WithName("GetDailyReport")
            .WithSummary("Returns the consolidated report for a selected day.");

        group
            .MapGet("/daily/export/csv", ExportDailyReportCsvAsync)
            .WithName("ExportDailyReportCsv")
            .WithSummary("Exports the consolidated daily report as CSV.");

        group
            .MapGet("/daily/export/pdf", ExportDailyReportPdfAsync)
            .WithName("ExportDailyReportPdf")
            .WithSummary("Exports the consolidated daily report as PDF.");

        return endpoints;
    }

    private static async Task<IResult> GetDailyReportAsync(
        DateOnly? date,
        ClaimsPrincipal user,
        IReportingService reportingService,
        CancellationToken cancellationToken
    )
    {
        var userId = user.ResolveCashFlowUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Problem(
                title: "Authenticated user is required",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var result = await reportingService.GetDailyReportAsync(
            new GetDailyReportRequest
            {
                UserId = userId,
                ReportDate = date ?? DateOnly.FromDateTime(DateTime.Today),
            },
            cancellationToken
        );

        return Results.Ok(result);
    }

    private static async Task<IResult> ExportDailyReportCsvAsync(
        DateOnly? date,
        ClaimsPrincipal user,
        IReportingService reportingService,
        ICsvReportExporter csvExporter,
        CancellationToken cancellationToken
    )
    {
        var userId = user.ResolveCashFlowUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Problem(
                title: "Authenticated user is required",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var report = await reportingService.GetDailyReportAsync(
            new GetDailyReportRequest
            {
                UserId = userId,
                ReportDate = date ?? DateOnly.FromDateTime(DateTime.Today),
            },
            cancellationToken
        );

        try
        {
            var export = csvExporter.Export(report);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception)
        {
            return Results.Problem(
                title: "CSV export failed",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private static async Task<IResult> ExportDailyReportPdfAsync(
        DateOnly? date,
        ClaimsPrincipal user,
        IReportingService reportingService,
        IPdfReportExporter pdfExporter,
        CancellationToken cancellationToken
    )
    {
        var userId = user.ResolveCashFlowUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Problem(
                title: "Authenticated user is required",
                statusCode: StatusCodes.Status401Unauthorized
            );
        }

        var report = await reportingService.GetDailyReportAsync(
            new GetDailyReportRequest
            {
                UserId = userId,
                ReportDate = date ?? DateOnly.FromDateTime(DateTime.Today),
            },
            cancellationToken
        );

        try
        {
            var export = pdfExporter.Export(report);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception)
        {
            return Results.Problem(
                title: "PDF export failed",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}
