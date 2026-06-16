using System.Net.Http.Headers;
using System.Net.Http.Json;
using CashFlow.Reporting.Application.Contracts;

namespace CashFlow.Web.Services;

public sealed class ReportingApiClient(HttpClient httpClient)
{
    public async Task<DailyReportResult?> GetDailyReportAsync(
        DateOnly reportDate,
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reports/daily?date={reportDate:yyyy-MM-dd}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<DailyReportResult>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<ReportDownloadResult?> DownloadDailyReportCsvAsync(
        DateOnly reportDate,
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/reports/daily/export/csv?date={reportDate:yyyy-MM-dd}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? $"daily-report-{reportDate:yyyy-MM-dd}.csv";
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/csv";

            return new ReportDownloadResult
            {
                Content = content,
                FileName = fileName,
                ContentType = contentType
            };
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

public sealed record ReportDownloadResult
{
    public required byte[] Content { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }
}