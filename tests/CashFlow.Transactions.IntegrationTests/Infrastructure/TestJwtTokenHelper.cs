using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Transactions.IntegrationTests.Infrastructure;

internal static class TestJwtTokenHelper
{
    public const string DefaultSigningKey = "dev-only-signing-key-change-me-1234567890";
    public const string DefaultIssuer = "CashFlow.Auth.Api";
    public const string DefaultAudience = "CashFlow.Web";
    public const string DefaultUserId = "integration-user@cashflow.local";

    public static string CreateToken(string userId = DefaultUserId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, userId),
            new("sub", userId)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(DefaultSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: DefaultIssuer,
            audience: DefaultAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void AuthorizeClient(HttpClient client, string userId = DefaultUserId)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(userId));
    }
}
