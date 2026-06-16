using System.Net.Http.Json;
using System.Text.Json;

namespace CashFlow.Transactions.Benchmarks.Http;

internal enum LoadTestAuthSource
{
    DevJwt,
    Provided,
    AutoLogin
}

internal sealed record ResolvedLoadTestAuth(
    string Token,
    LoadTestAuthSource Source,
    string? AuthBaseUrl = null)
{
    public string Describe() => Source switch
    {
        LoadTestAuthSource.Provided => "provided bearer token",
        LoadTestAuthSource.AutoLogin => $"auto-login ({AuthBaseUrl})",
        _ => "dev JWT (LoadTestJwtHelper)"
    };
}

internal static class LoadTestTokenResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ResolvedLoadTestAuth> ResolveAsync(
        string[] args,
        string? targetBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (CliArgParser.TryResolveExplicitToken(args, out var explicitToken))
        {
            return new ResolvedLoadTestAuth(explicitToken, LoadTestAuthSource.Provided);
        }

        if (string.IsNullOrWhiteSpace(targetBaseUrl))
        {
            return new ResolvedLoadTestAuth(LoadTestJwtHelper.CreateToken(), LoadTestAuthSource.DevJwt);
        }

        var candidates = CliArgParser.BuildAuthCredentialCandidates(args);
        Exception? lastError = null;

        foreach (var credentials in candidates)
        {
            try
            {
                Console.WriteLine($"Acquiring bearer token from {credentials.AuthBaseUrl} as {credentials.Email} ...");
                var token = await LoginAsync(credentials, cancellationToken);
                Console.WriteLine("Bearer token acquired successfully.");
                return new ResolvedLoadTestAuth(token, LoadTestAuthSource.AutoLogin, credentials.AuthBaseUrl);
            }
            catch (InvalidOperationException ex) when (IsInvalidCredentials(ex) && candidates.Count > 1)
            {
                lastError = ex;
                Console.WriteLine($"Login failed for {credentials.Email}, trying next account...");
            }
        }

        throw lastError ?? new InvalidOperationException("Auth login failed for all configured accounts.");
    }

    private static bool IsInvalidCredentials(InvalidOperationException exception) =>
        exception.Message.Contains("Invalid credentials", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("Invalid email or password", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> LoginAsync(LoadTestAuthCredentials credentials, CancellationToken cancellationToken)
    {
        using var httpClient = CreateAuthHttpClient(credentials.AuthBaseUrl);
        var loginUri = new Uri(httpClient.BaseAddress!, "api/auth/login");

        var initialRequest = new LoginPayload(credentials.Email, credentials.Password);
        using var challengeResponse = await httpClient.PostAsJsonAsync(loginUri, initialRequest, JsonOptions, cancellationToken);

        if (!challengeResponse.IsSuccessStatusCode)
        {
            var errorBody = await challengeResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Auth login failed ({(int)challengeResponse.StatusCode}) for '{credentials.Email}' at {loginUri}: {errorBody}");
        }

        var challenge = await challengeResponse.Content.ReadFromJsonAsync<LoginPayloadResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Auth login returned an empty response.");

        if (!string.IsNullOrWhiteSpace(challenge.Token))
        {
            return CliArgParser.NormalizeBearerToken(challenge.Token);
        }

        if (!challenge.RequiresMfa)
        {
            throw new InvalidOperationException("Auth login did not return a token or MFA challenge.");
        }

        if (string.IsNullOrWhiteSpace(challenge.ChallengeSession))
        {
            throw new InvalidOperationException("Auth MFA challenge is missing challengeSession.");
        }

        var mfaRequest = new LoginPayload(
            credentials.Email,
            credentials.Password,
            credentials.MfaCode,
            challenge.ChallengeSession,
            challenge.ChallengeName);

        using var tokenResponse = await httpClient.PostAsJsonAsync(loginUri, mfaRequest, JsonOptions, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Auth MFA login failed ({(int)tokenResponse.StatusCode}): {errorBody}");
        }

        var result = await tokenResponse.Content.ReadFromJsonAsync<LoginPayloadResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Auth MFA login returned an empty response.");

        if (string.IsNullOrWhiteSpace(result.Token))
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Auth MFA login did not return a token.");
        }

        return CliArgParser.NormalizeBearerToken(result.Token);
    }

    private static HttpClient CreateAuthHttpClient(string authBaseUrl)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(NormalizeBaseUrl(authBaseUrl)),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        return normalized.EndsWith('/') ? normalized : normalized + "/";
    }

    private sealed record LoginPayload(
        string Email,
        string Password,
        string? MfaCode = null,
        string? ChallengeSession = null,
        string? ChallengeName = null);

    private sealed record LoginPayloadResponse
    {
        public bool Succeeded { get; init; }

        public bool RequiresMfa { get; init; }

        public string? Token { get; init; }

        public string? ChallengeSession { get; init; }

        public string? ChallengeName { get; init; }

        public string? ErrorMessage { get; init; }
    }
}

internal sealed record LoadTestAuthCredentials(
    string AuthBaseUrl,
    string Email,
    string Password,
    string MfaCode);
