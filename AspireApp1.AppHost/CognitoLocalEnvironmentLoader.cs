namespace AspireApp1.AppHost;

internal static class CognitoLocalEnvironmentLoader
{
    public static bool TryLoad()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("CASHFLOW_COGNITO_ENABLED"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(
                Environment.GetEnvironmentVariable("CASHFLOW_AUTO_LOAD_COGNITO_LOCAL"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var envFile = ResolveEnvFilePath();
        if (envFile is null || !File.Exists(envFile))
        {
            return false;
        }

        foreach (var line in File.ReadAllLines(envFile))
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            Environment.SetEnvironmentVariable(key, value);
        }

        Environment.SetEnvironmentVariable("CASHFLOW_COGNITO_ENABLED", "true");
        Environment.SetEnvironmentVariable("Parameters__cognito-enabled", "true");
        Environment.SetEnvironmentVariable("Parameters__cognito-region", Environment.GetEnvironmentVariable("COGNITO_REGION") ?? "us-east-1");
        Environment.SetEnvironmentVariable("Parameters__cognito-service-url", Environment.GetEnvironmentVariable("COGNITO_SERVICE_URL") ?? "");
        Environment.SetEnvironmentVariable("Parameters__cognito-user-pool-id", Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID") ?? "");
        Environment.SetEnvironmentVariable("Parameters__cognito-client-id", Environment.GetEnvironmentVariable("COGNITO_CLIENT_ID") ?? "");
        Environment.SetEnvironmentVariable("Parameters__cognito-authentication-source", "CognitoLocal");
        Environment.SetEnvironmentVariable("DemoAccount__Email", Environment.GetEnvironmentVariable("COGNITO_USERNAME") ?? "admin@cashflow.docker");
        Environment.SetEnvironmentVariable("DemoAccount__Password", Environment.GetEnvironmentVariable("COGNITO_PASSWORD") ?? "Pass@word1");
        Environment.SetEnvironmentVariable("DemoAccount__MfaCode", Environment.GetEnvironmentVariable("COGNITO_MFA_CODE") ?? "123456");
        Environment.SetEnvironmentVariable("DemoAccount__Description", "Cognito Local demo account");

        return true;
    }

    private static string? ResolveEnvFilePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "infra", "cognito-local", "generated", "cognito.env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory.FullName, "AspireApp1.slnx")))
            {
                break;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
