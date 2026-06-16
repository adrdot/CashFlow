namespace CashFlow.Transactions.Benchmarks.Http;

internal static class CognitoLocalCredentialsLoader
{
    public static IReadOnlyDictionary<string, string> TryLoad()
    {
        var envFile = ResolveEnvFilePath();
        if (envFile is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

            values[parts[0].Trim()] = parts[1].Trim();
        }

        return values;
    }

    private static string? ResolveEnvFilePath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "infra",
                "cognito-local",
                "generated",
                "cognito.env");

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
