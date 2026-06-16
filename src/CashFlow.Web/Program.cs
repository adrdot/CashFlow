using Aspire.CashFlow.ServiceDefaults.Security;
using CashFlow.Web.Components;
using CashFlow.Web.Configuration;
using CashFlow.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.Configure<DemoAccountOptions>(
    builder.Configuration.GetSection(DemoAccountOptions.SectionName)
);
builder.Services.Configure<AuthSessionOptions>(
    builder.Configuration.GetSection(AuthSessionOptions.SectionName)
);
builder.Services.Configure<CognitoOAuthOptions>(
    builder.Configuration.GetSection(CognitoOAuthOptions.SectionName)
);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var authApiBaseAddress =
    builder.Configuration["AuthApi:BaseAddress"]
    ?? throw new InvalidOperationException("AuthApi:BaseAddress is required.");
var reportingApiBaseAddress =
    builder.Configuration["ReportingApi:BaseAddress"]
    ?? throw new InvalidOperationException("ReportingApi:BaseAddress is required.");
var transactionsApiBaseAddress =
    builder.Configuration["TransactionsApi:BaseAddress"]
    ?? throw new InvalidOperationException("TransactionsApi:BaseAddress is required.");

builder
    .Services.AddHttpClient<AuthApiClient>(client =>
    {
        client.BaseAddress = new Uri(authApiBaseAddress);
    })
    .AddServiceDiscovery();
builder
    .Services.AddHttpClient<ReportingApiClient>(client =>
    {
        client.BaseAddress = new Uri(reportingApiBaseAddress);
    })
    .AddServiceDiscovery();
builder
    .Services.AddHttpClient<TransactionsApiClient>(client =>
    {
        client.BaseAddress = new Uri(transactionsApiBaseAddress);
    })
    .AddServiceDiscovery();
builder.Services.AddScoped<SessionStore>();
builder.Services.AddScoped<AuthenticatedSessionService>();
builder.Services.AddScoped<OAuthLoginService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseCashFlowSecurity();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();
