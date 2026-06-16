using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CashFlow.Auth.Application.Contracts;

namespace CashFlow.Web.Services;

public sealed class AuthApiClient(HttpClient httpClient)
{
    public async Task<OAuthTokenResponse?> ExchangeOAuthCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/api/auth/oauth/token",
                new OAuthTokenRequest
                {
                    GrantType = "authorization_code",
                    Code = code,
                    RedirectUri = redirectUri,
                },
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(
                cancellationToken: cancellationToken
            );
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/api/auth/login",
                request,
                cancellationToken
            );
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<LoginResult>(
                        cancellationToken: cancellationToken
                    ) ?? LoginResult.Failure("Authentication service returned an empty response.");
            }

            var errorMessage = await ReadProblemDetailAsync(response, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return LoginResult.Failure(errorMessage ?? "Invalid email or password.");
            }

            return LoginResult.Failure(
                errorMessage ?? "Authentication service is unavailable. Try again later."
            );
        }
        catch (HttpRequestException)
        {
            return LoginResult.Failure("Authentication service is unavailable. Try again later.");
        }
    }

    public async Task<LoginResult> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshTokenRequest { RefreshToken = refreshToken },
                cancellationToken
            );

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<LoginResult>(
                        cancellationToken: cancellationToken
                    ) ?? LoginResult.Failure("Authentication service returned an empty response.");
            }

            var errorMessage = await ReadProblemDetailAsync(response, cancellationToken);
            return LoginResult.Failure(errorMessage ?? "Refresh token is invalid or expired.");
        }
        catch (HttpRequestException)
        {
            return LoginResult.Failure("Authentication service is unavailable. Try again later.");
        }
    }

    public async Task<SessionState?> ValidateSessionAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SessionState>(
                cancellationToken: cancellationToken
            );
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Local logout still proceeds even if the auth API is unavailable.
        }
    }

    private static async Task<string?> ReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken
            );
            if (
                document.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String
            )
            {
                return detail.GetString();
            }

            if (
                document.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String
            )
            {
                return title.GetString();
            }
        }
        catch (JsonException) { }

        return null;
    }
}
