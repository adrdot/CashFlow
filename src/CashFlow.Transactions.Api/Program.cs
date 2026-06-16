using Aspire.CashFlow.ServiceDefaults;
using Aspire.CashFlow.ServiceDefaults.Authentication;
using Aspire.CashFlow.ServiceDefaults.Security;
using CashFlow.Transactions.Api.Configuration;
using CashFlow.Transactions.Api.Endpoints;
using CashFlow.Transactions.Api.HealthChecks;
using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.UseCases;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddCashFlowJwtAuthentication(builder.Configuration);
builder.Services.AddTransactionsInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddScoped<ITransactionService, CreateTransactionHandler>();

var eventStoreOptions = builder
    .Configuration.GetSection(EventStoreOptions.SectionName)
    .Get<EventStoreOptions>();
if (eventStoreOptions is not null && !string.IsNullOrWhiteSpace(eventStoreOptions.HttpEndpoint))
{
    builder.Services.AddHttpClient(
        "eventstore",
        client =>
        {
            client.BaseAddress = new Uri(eventStoreOptions.HttpEndpoint.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(eventStoreOptions.HttpTimeoutSeconds);
        }
    );
    builder
        .Services.AddHealthChecks()
        .AddCheck<EventStoreHealthCheck>("eventstore", tags: ["ready"]);
}

var app = builder.Build();

_ = app.Services.GetRequiredService<TransactionMetrics>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCashFlowApiPipeline();
app.UseCashFlowApiAuthentication();

app.MapTransactionEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
