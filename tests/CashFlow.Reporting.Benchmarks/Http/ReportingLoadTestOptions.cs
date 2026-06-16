using CashFlow.Transactions.Benchmarks.Http;

namespace CashFlow.Reporting.Benchmarks.Http;

internal static class ReportingLoadTestDefaults
{
    public const string DefaultBaseUrl = "https://localhost:7090";

    public const int DefaultLoadRate = 50;

    public const int DefaultLoadDurationSeconds = 30;
}

internal sealed class ReportingLoadTestOptions
{
    public string BaseUrl { get; init; } = ReportingLoadTestDefaults.DefaultBaseUrl;

    public string BearerToken { get; init; } = string.Empty;

    public string AuthDescription { get; init; } = "unknown";

    public int Rate { get; init; } = ReportingLoadTestDefaults.DefaultLoadRate;

    public int DurationSeconds { get; init; } = ReportingLoadTestDefaults.DefaultLoadDurationSeconds;

    public DateOnly? ReportDate { get; init; }

    public static async Task<ReportingLoadTestOptions> ParseAsync(string[] args)
    {
        var shared = await LoadTestOptions.ParseAsync(args);
        var reportDateRaw = CliArgParser.ParseString(args, "--report-date");
        DateOnly? reportDate = DateOnly.TryParse(reportDateRaw, out var parsed)
            ? parsed
            : new DateOnly(2026, 6, 12);

        return new ReportingLoadTestOptions
        {
            BaseUrl = ReportingBaseUrlResolver.Resolve(shared.BaseUrl),
            BearerToken = shared.BearerToken,
            AuthDescription = shared.DescribeAuth(),
            Rate = shared.Rate > 0 ? shared.Rate : ReportingLoadTestDefaults.DefaultLoadRate,
            DurationSeconds = shared.DurationSeconds > 0
                ? shared.DurationSeconds
                : ReportingLoadTestDefaults.DefaultLoadDurationSeconds,
            ReportDate = reportDate
        };
    }

    public string DescribeAuth() => AuthDescription;
}

internal static class ReportingBaseUrlResolver
{
    public static string Resolve(string? cliUrl)
    {
        if (IsUsableUrl(cliUrl))
        {
            return cliUrl!.TrimEnd('/');
        }

        var envUrl = Environment.GetEnvironmentVariable("CASHFLOW_REPORTING_URL");
        if (IsUsableUrl(envUrl))
        {
            return envUrl!.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(cliUrl)
            && cliUrl.Contains("PORT", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"Warning: --url '{cliUrl}' looks like a placeholder. " +
                $"Using {ReportingLoadTestDefaults.DefaultBaseUrl}. " +
                "Set CASHFLOW_REPORTING_URL or pass a real Aspire dashboard URL.");
        }

        return ReportingLoadTestDefaults.DefaultBaseUrl;
    }

    private static bool IsUsableUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("PORT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host.Length > 0
            && uri.Port > 0;
    }
}
