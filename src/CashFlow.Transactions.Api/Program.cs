using AspireApp1.ServiceDefaults.Authentication;
using AspireApp1.ServiceDefaults.Security;
using CashFlow.Transactions.Api.Configuration;
using CashFlow.Transactions.Api.Endpoints;
using CashFlow.Transactions.Api.HealthChecks;
using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.UseCases;
using CashFlow.Transactions.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddCashFlowJwtAuthentication(builder.Configuration);
builder.Services.AddTransactionsInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddScoped<ITransactionService, CreateTransactionHandler>();

var eventStoreEndpoint = builder.Configuration["EventStore:HttpEndpoint"];
if (!string.IsNullOrWhiteSpace(eventStoreEndpoint))
{
    builder.Services.AddHttpClient("eventstore", client =>
    {
        client.BaseAddress = new Uri(eventStoreEndpoint.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddHealthChecks()
        .AddCheck<EventStoreHealthCheck>("eventstore", tags: ["ready"]);
}

var app = builder.Build();

_ = app.Services.GetRequiredService<TransactionMetrics>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseCashFlowHttpsRedirection();
app.UseCashFlowSecurity();
app.UseMiddleware<TransactionObservabilityMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapTransactionEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
