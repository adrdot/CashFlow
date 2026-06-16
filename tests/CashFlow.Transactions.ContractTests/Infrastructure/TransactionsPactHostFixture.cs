using System.Net;
using System.Text.Json;
using AspireApp1.ServiceDefaults.Authentication;
using CashFlow.Transactions.Api.Endpoints;
using CashFlow.Transactions.Application.Abstractions;
using CashFlow.Transactions.Application.UseCases;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CashFlow.Transactions.ContractTests.Infrastructure;

public sealed class TransactionsPactHostFixture : IAsyncLifetime, IDisposable
{
    private WebApplication? application;
    private SwitchableTransactionRepository? switchableRepository;

    public Uri ServerUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(PactConstants.PactDirectory);

        var port = GetFreeTcpPort();
        ServerUri = new Uri($"http://127.0.0.1:{port}");
        switchableRepository = new SwitchableTransactionRepository();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseUrls(ServerUri.ToString());

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:transactions-db"] = string.Empty,
            ["EventStore:HttpEndpoint"] = string.Empty,
            ["Messaging:Enabled"] = "false",
            ["Cognito:Enabled"] = "false",
            ["Jwt:Issuer"] = TestJwtTokenHelper.DefaultIssuer,
            ["Jwt:Audience"] = TestJwtTokenHelper.DefaultAudience,
            ["Jwt:SigningKey"] = TestJwtTokenHelper.DefaultSigningKey
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddCashFlowJwtAuthentication(builder.Configuration);
        builder.Services.RemoveAll<ITransactionRepository>();
        builder.Services.AddSingleton(switchableRepository);
        builder.Services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<SwitchableTransactionRepository>());
        builder.Services.AddSingleton<ITransactionEventPublisher, NullTransactionEventPublisher>();
        builder.Services.AddScoped<ITransactionService, CreateTransactionHandler>();

        application = builder.Build();

        application.UseMiddleware<PactProviderStateMiddleware>(switchableRepository);
        application.UseMiddleware<PactAuthTokenRequestFilter>();
        application.UseExceptionHandler();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapTransactionEndpoints();

        await application.StartAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (application is null)
        {
            return;
        }

        application.StopAsync().GetAwaiter().GetResult();
        application.DisposeAsync().GetAwaiter().GetResult();
    }

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed class PactProviderStateMiddleware
{
    private readonly RequestDelegate next;
    private readonly SwitchableTransactionRepository repository;
    private readonly Dictionary<string, Action> providerStates;

    public PactProviderStateMiddleware(RequestDelegate next, SwitchableTransactionRepository repository)
    {
        this.next = next;
        this.repository = repository;
        providerStates = new Dictionary<string, Action>(StringComparer.Ordinal)
        {
            ["a user is authenticated"] = () => repository.PersistenceFails = false,
            ["transaction persistence fails"] = () => repository.PersistenceFails = true,
            ["no bearer token is provided"] = () => repository.PersistenceFails = false
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/provider-states" &&
            HttpMethods.IsPost(context.Request.Method))
        {
            var providerState = await JsonSerializer.DeserializeAsync<ProviderState>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (providerState?.State is not null &&
                providerStates.TryGetValue(providerState.State, out var action))
            {
                action();
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            return;
        }

        await next(context);
    }

    private sealed record ProviderState(string? State);
}

internal sealed class PactAuthTokenRequestFilter
{
    private const string AuthorizationHeaderKey = "Authorization";
    private readonly RequestDelegate next;

    public PactAuthTokenRequestFilter(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey(AuthorizationHeaderKey))
        {
            context.Request.Headers[AuthorizationHeaderKey] =
                $"Bearer {TestJwtTokenHelper.CreateToken()}";
        }

        await next(context);
    }
}
