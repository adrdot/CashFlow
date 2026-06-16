namespace CashFlow.Transactions.IntegrationTests.Infrastructure;

internal static class TransactionsTestEnvironment
{
    public static void IsolateFromAspireEnvironment()
    {
        Environment.SetEnvironmentVariable("Cognito__Enabled", "false");
        Environment.SetEnvironmentVariable("Cognito__UserPoolId", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__ClientId", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__ServiceUrl", string.Empty);
        Environment.SetEnvironmentVariable("CloudWatch__Enabled", "false");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
    }
}
