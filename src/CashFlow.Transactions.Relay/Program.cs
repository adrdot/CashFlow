using CashFlow.Transactions.Api.Configuration;
using CashFlow.Transactions.Infrastructure.EventStore;
using CashFlow.Transactions.Infrastructure.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddTransactionsRelayInfrastructure(builder.Configuration);

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
}

var host = builder.Build();

_ = host.Services.GetRequiredService<TransactionMetrics>();

host.Run();

public partial class Program;
