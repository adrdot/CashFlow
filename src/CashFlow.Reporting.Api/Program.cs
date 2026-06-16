using AspireApp1.ServiceDefaults.Authentication;
using AspireApp1.ServiceDefaults.Security;
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

app.UseExceptionHandler();
app.UseCashFlowHttpsRedirection();
app.UseCashFlowSecurity();
app.UseMiddleware<ReportingObservabilityMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapReportingEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
