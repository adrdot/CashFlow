using CashFlow.Reporting.Api.Configuration;
using CashFlow.Reporting.Infrastructure.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddReportingProjectionInfrastructure(builder.Configuration);

var host = builder.Build();

_ = host.Services.GetRequiredService<ReportingMetrics>();

host.Run();

public partial class Program;
