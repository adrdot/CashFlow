using CashFlow.Transactions.Api.Configuration;
using CashFlow.Transactions.Infrastructure.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddTransactionsRelayInfrastructure(builder.Configuration);

var eventStoreEndpoint = builder.Configuration["EventStore:HttpEndpoint"];
if (!string.IsNullOrWhiteSpace(eventStoreEndpoint))
{
    builder.Services.AddHttpClient("eventstore", client =>
    {
        client.BaseAddress = new Uri(eventStoreEndpoint.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
}

var host = builder.Build();

_ = host.Services.GetRequiredService<TransactionMetrics>();

host.Run();

public partial class Program;
