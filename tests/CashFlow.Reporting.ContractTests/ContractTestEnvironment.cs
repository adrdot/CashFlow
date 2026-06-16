namespace CashFlow.Reporting.ContractTests;

internal static class ContractTestEnvironment
{
    internal static void IsolateFromLocalStack()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__reporting-db", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__Enabled", "false");
        Environment.SetEnvironmentVariable("Cognito__UserPoolId", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__ClientId", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__ServiceUrl", string.Empty);
        Environment.SetEnvironmentVariable("CloudWatch__Enabled", "false");
        Environment.SetEnvironmentVariable("Reporting__Redis__Enabled", "false");
        Environment.SetEnvironmentVariable("Reporting__Redis__Configuration", string.Empty);
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
    }
}
