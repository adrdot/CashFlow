namespace CashFlow.FunctionalTests.Infrastructure;

internal static class FunctionalTestEnvironment
{
    private static readonly string? OriginalReportingConnection =
        Environment.GetEnvironmentVariable("ConnectionStrings__reporting-db");

    private static readonly string? OriginalCognitoEnabled = Environment.GetEnvironmentVariable(
        "Cognito__Enabled"
    );

    public static void IsolateFromLocalStack()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__reporting-db", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__Enabled", "false");
        Environment.SetEnvironmentVariable("Cognito__UserPoolId", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__ClientId", string.Empty);
        Environment.SetEnvironmentVariable("Cognito__ServiceUrl", string.Empty);
    }

    public static void Restore()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__reporting-db",
            OriginalReportingConnection
        );
        Environment.SetEnvironmentVariable("Cognito__Enabled", OriginalCognitoEnabled);
    }
}
