using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CashFlow.Reporting.Application.Contracts;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Reporting.ContractTests;

public sealed class GetDailyReportContractTests
{
    [Fact]
    public async Task GetDailyReport_ReturnsOk_WithAuthenticatedUser()
    {
        await using var factory = new ReportingWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateTestToken("dev-user")
        );

        var response = await client.GetAsync("/api/reports/daily?date=2026-06-12");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DailyReportResult>();
        Assert.NotNull(payload);
        Assert.Equal(new DateOnly(2026, 6, 12), payload!.ReportDate);
    }

    private static string CreateTestToken(string userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, userId),
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("dev-only-signing-key-change-me-1234567890")
        );
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "CashFlow.Auth.Api",
            audience: "CashFlow.Web",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
