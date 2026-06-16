using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CashFlow.Auth.Application.Abstractions;
using CashFlow.Auth.Application.Contracts;
using CashFlow.Auth.Domain.Entities;
using CashFlow.Auth.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Auth.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private const string RefreshTokenUseClaim = "token_use";

    private readonly JwtOptions jwtOptions = options.Value;

    public string CreateToken(UserAccount userAccount)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userAccount.Id.ToString()),
            new(ClaimTypes.NameIdentifier, userAccount.Id.ToString()),
            new(ClaimTypes.Email, userAccount.Email),
            new(ClaimTypes.Name, userAccount.DisplayName)
        };

        var signingCredentials = new SigningCredentials(GetSigningKey(jwtOptions), SecurityAlgorithms.HmacSha256);
        var jwtToken = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }

    public string CreateRefreshToken(UserAccount userAccount)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddDays(jwtOptions.RefreshTokenExpirationDays);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userAccount.Id.ToString()),
            new(ClaimTypes.NameIdentifier, userAccount.Id.ToString()),
            new(ClaimTypes.Email, userAccount.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(RefreshTokenUseClaim, "refresh")
        };

        var signingCredentials = new SigningCredentials(GetSigningKey(jwtOptions), SecurityAlgorithms.HmacSha256);
        var jwtToken = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }

    public SessionState? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, CreateValidationParameters(jwtOptions), out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return null;
            }

            return new SessionState
            {
                Email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                DisplayName = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                ExpiresAtUtc = new DateTimeOffset(jwtToken.ValidTo, TimeSpan.Zero)
            };
        }
        catch
        {
            return null;
        }
    }

    public string? ValidateRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(refreshToken, CreateValidationParameters(jwtOptions), out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return null;
            }

            var tokenUse = principal.FindFirstValue(RefreshTokenUseClaim);
            if (!string.Equals(tokenUse, "refresh", StringComparison.Ordinal))
            {
                return null;
            }

            return principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);
        }
        catch
        {
            return null;
        }
    }

    public static TokenValidationParameters CreateValidationParameters(JwtOptions options)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetSigningKey(options),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    private static SymmetricSecurityKey GetSigningKey(JwtOptions options)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
    }
}