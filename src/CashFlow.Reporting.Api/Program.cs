using Aspire.CashFlow.ServiceDefaults;
using Aspire.CashFlow.ServiceDefaults.Authentication;
using Aspire.CashFlow.ServiceDefaults.Security;
using CashFlow.Reporting.Api.Configuration;
using CashFlow.Reporting.Api.Endpoints;
using CashFlow.Reporting.Application.Abstractions;
using CashFlow.Reporting.Application.UseCases;
using CashFlow.Reporting.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddCashFlowJwtAuthentication(builder.Configuration);
builder.Services.AddReportingInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddScoped<IReportingService, GetDailyReportHandler>();

var app = builder.Build();

_ = app.Services.GetRequiredService<ReportingMetrics>();

await app.ApplyReportingMigrationsAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCashFlowApiPipeline();
app.UseCashFlowApiAuthentication();

app.MapReportingEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
