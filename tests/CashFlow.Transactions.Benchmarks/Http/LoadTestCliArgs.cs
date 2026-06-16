namespace CashFlow.Transactions.Benchmarks.Http;

internal sealed record LoadTestOptions(
    string? BaseUrl,
    string BearerToken,
    LoadTestAuthSource AuthSource,
    string? AuthBaseUrl,
    int Rate,
    int DurationSeconds
)
{
    public static async Task<LoadTestOptions> ParseAsync(
        string[] args,
        CancellationToken cancellationToken = default
    )
    {
        var baseUrl = CliArgParser.ParseString(args, "--url");
        var auth = await LoadTestTokenResolver.ResolveAsync(args, baseUrl, cancellationToken);

        return new(
            BaseUrl: baseUrl,
            BearerToken: auth.Token,
            AuthSource: auth.Source,
            AuthBaseUrl: auth.AuthBaseUrl,
            Rate: CliArgParser.ParseInt(
                args,
                "--rate",
                TransactionLoadTestDefaults.DefaultLoadRate
            ),
            DurationSeconds: CliArgParser.ParseInt(
                args,
                "--duration",
                TransactionLoadTestDefaults.DefaultLoadDurationSeconds
            )
        );
    }

    public string DescribeAuth() =>
        new ResolvedLoadTestAuth(BearerToken, AuthSource, AuthBaseUrl).Describe();
}

internal sealed record StressTestOptions(
    string? BaseUrl,
    string BearerToken,
    LoadTestAuthSource AuthSource,
    string? AuthBaseUrl,
    int StartRate,
    int StepRate,
    int MaxRate,
    int StepSeconds,
    int StepPauseSeconds,
    double FailureThresholdPercent,
    int MaxMeanLatencyMs
)
{
    public const int DefaultStepPauseSeconds = 61;

    public static async Task<StressTestOptions> ParseAsync(
        string[] args,
        CancellationToken cancellationToken = default
    )
    {
        var baseUrl = CliArgParser.ParseString(args, "--url");
        var auth = await LoadTestTokenResolver.ResolveAsync(args, baseUrl, cancellationToken);

        return new(
            BaseUrl: baseUrl,
            BearerToken: auth.Token,
            AuthSource: auth.Source,
            AuthBaseUrl: auth.AuthBaseUrl,
            StartRate: CliArgParser.ParseInt(
                args,
                "--start-rate",
                TransactionLoadTestDefaults.DefaultStressStartRate
            ),
            StepRate: CliArgParser.ParseInt(
                args,
                "--step",
                TransactionLoadTestDefaults.DefaultStressStepRate
            ),
            MaxRate: CliArgParser.ParseInt(
                args,
                "--max-rate",
                TransactionLoadTestDefaults.DefaultStressMaxRate
            ),
            StepSeconds: CliArgParser.ParseInt(
                args,
                "--step-duration",
                TransactionLoadTestDefaults.DefaultStressStepSeconds
            ),
            StepPauseSeconds: CliArgParser.ParseInt(args, "--step-pause", DefaultStepPauseSeconds),
            FailureThresholdPercent: CliArgParser.ParseDouble(
                args,
                "--failure-threshold",
                TransactionLoadTestDefaults.DefaultStressFailureThresholdPercent
            ),
            MaxMeanLatencyMs: CliArgParser.ParseInt(
                args,
                "--max-mean-latency",
                TransactionLoadTestSloGates.MaxMeanLatencyMs
            )
        );
    }

    public string DescribeAuth() =>
        new ResolvedLoadTestAuth(BearerToken, AuthSource, AuthBaseUrl).Describe();
}

internal static class CliArgParser
{
    private const string DefaultAuthBaseUrl = "https://localhost:7204";
    private const string DefaultPassword = "Pass@word1";
    private const string DefaultMfaCode = "123456";

    public static bool TryResolveExplicitToken(string[] args, out string token)
    {
        token = string.Empty;

        var inlineToken = ParseString(args, "--token");
        if (!string.IsNullOrWhiteSpace(inlineToken))
        {
            token = NormalizeBearerToken(inlineToken);
            return true;
        }

        var tokenFile = ParseString(args, "--token-file");
        if (!string.IsNullOrWhiteSpace(tokenFile))
        {
            var path = Path.GetFullPath(tokenFile);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Token file not found: {path}{Environment.NewLine}"
                        + $"Current directory: {Directory.GetCurrentDirectory()}{Environment.NewLine}"
                        + "Create the file with a single access token line, or omit --token-file to auto-login.",
                    path
                );
            }

            var fileToken = File.ReadAllText(path).Trim();
            if (string.IsNullOrWhiteSpace(fileToken))
            {
                throw new InvalidOperationException($"Token file is empty: {path}");
            }

            token = NormalizeBearerToken(fileToken);
            return true;
        }

        var tokenEnvVar = ParseString(args, "--token-env");
        if (!string.IsNullOrWhiteSpace(tokenEnvVar))
        {
            var envToken = Environment.GetEnvironmentVariable(tokenEnvVar);
            if (string.IsNullOrWhiteSpace(envToken))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{tokenEnvVar}' is not set or is empty."
                );
            }

            token = NormalizeBearerToken(envToken);
            return true;
        }

        return false;
    }

    public static LoadTestAuthCredentials ResolveAuthCredentials(string[] args) =>
        BuildAuthCredentialCandidates(args)[0];

    public static bool HasExplicitAuthOverrides(string[] args) =>
        !string.IsNullOrWhiteSpace(ParseString(args, "--auth-email"))
        || !string.IsNullOrWhiteSpace(ParseString(args, "--auth-password"))
        || !string.IsNullOrWhiteSpace(ParseString(args, "--auth-mfa-code"))
        || !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("CASHFLOW_LOAD_TEST_EMAIL")
        )
        || !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("CASHFLOW_LOAD_TEST_PASSWORD")
        )
        || !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("CASHFLOW_LOAD_TEST_MFA_CODE")
        );

    public static IReadOnlyList<LoadTestAuthCredentials> BuildAuthCredentialCandidates(
        string[] args
    )
    {
        var authBaseUrl = FirstNonEmpty(
            ParseString(args, "--auth-url"),
            Environment.GetEnvironmentVariable("CASHFLOW_AUTH_URL"),
            DefaultAuthBaseUrl
        )!;

        var password = FirstNonEmpty(
            ParseString(args, "--auth-password"),
            Environment.GetEnvironmentVariable("CASHFLOW_LOAD_TEST_PASSWORD"),
            Environment.GetEnvironmentVariable("COGNITO_PASSWORD"),
            Environment.GetEnvironmentVariable("DemoAccount__Password"),
            DefaultPassword
        )!;

        var mfaCode = FirstNonEmpty(
            ParseString(args, "--auth-mfa-code"),
            Environment.GetEnvironmentVariable("CASHFLOW_LOAD_TEST_MFA_CODE"),
            Environment.GetEnvironmentVariable("COGNITO_MFA_CODE"),
            Environment.GetEnvironmentVariable("DemoAccount__MfaCode"),
            DefaultMfaCode
        )!;

        if (HasExplicitAuthOverrides(args))
        {
            var email = FirstNonEmpty(
                ParseString(args, "--auth-email"),
                Environment.GetEnvironmentVariable("CASHFLOW_LOAD_TEST_EMAIL"),
                "admin@cashflow.docker"
            )!;

            return [new LoadTestAuthCredentials(authBaseUrl, email, password, mfaCode)];
        }

        var cognitoEnv = CognitoLocalCredentialsLoader.TryLoad();
        var candidates = new List<LoadTestAuthCredentials>();

        AddCandidate(
            candidates,
            authBaseUrl,
            Environment.GetEnvironmentVariable("COGNITO_USERNAME"),
            password,
            mfaCode
        );
        AddCandidate(
            candidates,
            authBaseUrl,
            GetCognitoValue(cognitoEnv, "COGNITO_USERNAME"),
            password,
            mfaCode
        );
        AddCandidate(
            candidates,
            authBaseUrl,
            Environment.GetEnvironmentVariable("DemoAccount__Email"),
            password,
            mfaCode
        );
        AddCandidate(candidates, authBaseUrl, "admin@cashflow.docker", password, mfaCode);
        AddCandidate(candidates, authBaseUrl, "admin@cashflow.local", password, mfaCode);

        return candidates;
    }

    private static void AddCandidate(
        List<LoadTestAuthCredentials> candidates,
        string authBaseUrl,
        string? email,
        string password,
        string mfaCode
    )
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        if (
            candidates.Any(candidate =>
                string.Equals(candidate.Email, email, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return;
        }

        candidates.Add(new LoadTestAuthCredentials(authBaseUrl, email, password, mfaCode));
    }

    public static int ParseInt(string[] args, string name, int defaultValue)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (
                string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[index + 1], out var value)
            )
            {
                return value;
            }
        }

        return defaultValue;
    }

    public static double ParseDouble(string[] args, string name, double defaultValue)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (
                string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)
                && double.TryParse(args[index + 1], out var value)
            )
            {
                return value;
            }
        }

        return defaultValue;
    }

    public static string? ParseString(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static string? GetCognitoValue(
        IReadOnlyDictionary<string, string> cognitoEnv,
        string key
    ) => cognitoEnv.TryGetValue(key, out var value) ? value : null;

    public static string NormalizeBearerToken(string token)
    {
        var normalized = token.Trim();
        const string bearerPrefix = "Bearer ";
        if (normalized.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[bearerPrefix.Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Bearer token cannot be empty.");
        }

        return normalized;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
